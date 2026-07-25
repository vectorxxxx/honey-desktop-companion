param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [double]$DurationHours = 8,
    [int]$DurationSeconds = 0,
    [int]$SampleSeconds = 30,
    [int]$WarmupSeconds = 60,
    [string]$DataRoot,
    [string]$ReportPath,
    [switch]$KeepData
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "soak-metrics.ps1")
. (Join-Path $PSScriptRoot "soak-lifecycle.ps1")
if ($SampleSeconds -lt 1) { throw "SampleSeconds must be at least 1." }
$duration = if ($DurationSeconds -gt 0) {
    [TimeSpan]::FromSeconds($DurationSeconds)
} else { [TimeSpan]::FromHours($DurationHours) }
if ($duration.TotalSeconds -le ($WarmupSeconds + $SampleSeconds)) {
    throw "Duration must allow at least two samples after warmup."
}

$executable = (Resolve-Path -LiteralPath $ExePath -ErrorAction Stop).Path
$ownsDataRoot = [string]::IsNullOrWhiteSpace($DataRoot)
if ($ownsDataRoot) {
    $DataRoot = Join-Path ([IO.Path]::GetTempPath()) ("honey-soak-" + [Guid]::NewGuid().ToString("N"))
}
$DataRoot = [IO.Path]::GetFullPath($DataRoot)
$instanceId = "soak" + [Guid]::NewGuid().ToString("N")
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\soak"
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    $ReportPath = Join-Path $reportDirectory ("soak-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".csv")
}
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$process = $null
$startTimeUtc = $null
$mainFailure = $null
$cleanupFailure = $null
$finalIdentityAlive = $false
$shutdownExitCode = -1
$residual = $false

try {
    $process = Start-Process -FilePath $executable `
        -ArgumentList @("--background", "--data-root", "`"$DataRoot`"", "--instance-id", $instanceId) `
        -PassThru
    $startTimeUtc = $process.StartTime.ToUniversalTime()
    Start-Sleep -Seconds $WarmupSeconds
    if (-not (Test-SoakIdentity -ProcessId $process.Id -StartTimeUtc $startTimeUtc `
        -ExePath $executable -InstanceId $instanceId)) {
        throw "Honey exited or changed identity during warmup."
    }

    $samples = [Collections.Generic.List[object]]::new()
    $start = [DateTime]::UtcNow
    $previousAt = $start
    $previousCpu = $process.TotalProcessorTime
    $logicalProcessors = [Math]::Max(1, [Environment]::ProcessorCount)
    while (([DateTime]::UtcNow - $start) -lt $duration) {
        Start-Sleep -Seconds $SampleSeconds
        if (-not (Test-SoakIdentity -ProcessId $process.Id -StartTimeUtc $startTimeUtc `
            -ExePath $executable -InstanceId $instanceId)) {
            throw "Honey exited or changed identity during soak test."
        }
        $process.Refresh()
        $now = [DateTime]::UtcNow
        $cpu = $process.TotalProcessorTime
        $wallSeconds = ($now - $previousAt).TotalSeconds
        $samples.Add([pscustomobject]@{
            TimestampUtc = $now.ToString("O")
            ElapsedSeconds = [Math]::Round(($now - $start).TotalSeconds, 3)
            CpuPercent = [Math]::Round(
                (($cpu - $previousCpu).TotalSeconds / $wallSeconds / $logicalProcessors) * 100, 3)
            WorkingSetMb = [Math]::Round($process.WorkingSet64 / 1MB, 3)
        })
        $previousAt = $now
        $previousCpu = $cpu
    }

    # 指标采集完成后再做一次独立身份确认，捕获“最后一次采样后立即崩溃”。
    $finalIdentityAlive = Test-SoakIdentity -ProcessId $process.Id -StartTimeUtc $startTimeUtc `
        -ExePath $executable -InstanceId $instanceId
    if (-not $finalIdentityAlive) { throw "Honey failed final identity confirmation." }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null
    $samples | Export-Csv -LiteralPath $ReportPath -NoTypeInformation -Encoding UTF8
    $result = Get-SoakAcceptance -Samples $samples
    if (-not $result.Passed) { throw "Soak acceptance thresholds were not met." }
}
catch {
    $mainFailure = $_
}
finally {
    if ($null -ne $process -and $null -ne $startTimeUtc) {
        try {
            $stopper = Start-Process -FilePath $executable `
                -ArgumentList @("--shutdown", "--data-root", "`"$DataRoot`"", "--instance-id", $instanceId) `
                -PassThru
            $stopperStartTimeUtc = $stopper.StartTime.ToUniversalTime()
            if (-not $stopper.WaitForExit(15000)) {
                $shutdownExitCode = -2
                if (Test-SoakIdentity -ProcessId $stopper.Id `
                    -StartTimeUtc $stopperStartTimeUtc -ExePath $executable -InstanceId $instanceId) {
                    Stop-Process -Id $stopper.Id -Force
                }
            } else {
                $shutdownExitCode = $stopper.ExitCode
            }
            $process.WaitForExit(15000) | Out-Null
            $residual = Test-SoakIdentity -ProcessId $process.Id -StartTimeUtc $startTimeUtc `
                -ExePath $executable -InstanceId $instanceId
            if ($residual) {
                # 只强制清理经 PID、启动时间、EXE 路径和实例令牌四重确认的本次进程。
                Stop-Process -Id $process.Id -Force
            }
            Assert-SoakLifecycleOutcome -FinalIdentityAlive:$finalIdentityAlive `
                -ShutdownExitCode $shutdownExitCode -ResidualExactInstance:$residual
        }
        catch { $cleanupFailure = $_ }
    }
    if ($ownsDataRoot -and -not $KeepData -and (Test-Path -LiteralPath $DataRoot)) {
        $resolved = [IO.Path]::GetFullPath($DataRoot)
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $resolved).StartsWith("honey-soak-")) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}

if ($null -ne $mainFailure -and $null -ne $cleanupFailure) {
    throw "长时验收失败：$($mainFailure.Exception.Message)；清理也失败：$($cleanupFailure.Exception.Message)"
}
if ($null -ne $cleanupFailure) { throw $cleanupFailure }
if ($null -ne $mainFailure) { throw $mainFailure }
Write-Host ("Soak passed. Report: {0}" -f $ReportPath)
