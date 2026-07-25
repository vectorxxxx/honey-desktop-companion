param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
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

if ($SampleSeconds -lt 1) { throw "SampleSeconds must be at least 1." }
$duration = if ($DurationSeconds -gt 0) {
    [TimeSpan]::FromSeconds($DurationSeconds)
}
else {
    [TimeSpan]::FromHours($DurationHours)
}
if ($duration.TotalSeconds -le ($WarmupSeconds + $SampleSeconds)) {
    throw "Duration must allow at least two samples after warmup."
}

$executable = (Resolve-Path -LiteralPath $ExePath -ErrorAction Stop).Path
$ownsDataRoot = [string]::IsNullOrWhiteSpace($DataRoot)
if ($ownsDataRoot) {
    $DataRoot = Join-Path ([IO.Path]::GetTempPath()) ("honey-soak-" + [Guid]::NewGuid().ToString("N"))
}
$DataRoot = [IO.Path]::GetFullPath($DataRoot)
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\soak"
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    $ReportPath = Join-Path $reportDirectory ("soak-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".csv")
}
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$process = $null

function Stop-SoakProcess {
    if ($null -eq $process) { return }
    $running = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    if ($null -eq $running) { return }

    $stopper = Start-Process -FilePath $executable `
        -ArgumentList @("--shutdown", "--data-root", "`"$DataRoot`"") `
        -PassThru
    $stopper.WaitForExit(15000) | Out-Null
    $running.WaitForExit(15000) | Out-Null
    if (-not $running.HasExited) {
        $candidate = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($null -ne $candidate -and
            [string]::Equals($candidate.Path, $executable, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $process.Id -Force
        }
        throw "Soak process did not shut down cleanly."
    }
}

try {
    $process = Start-Process -FilePath $executable `
        -ArgumentList @("--background", "--data-root", "`"$DataRoot`"") `
        -PassThru
    Start-Sleep -Seconds $WarmupSeconds
    $process.Refresh()
    if ($process.HasExited) { throw "Honey exited during warmup." }

    $samples = [Collections.Generic.List[object]]::new()
    $start = [DateTime]::UtcNow
    $previousAt = $start
    $previousCpu = $process.TotalProcessorTime
    $logicalProcessors = [Math]::Max(1, [Environment]::ProcessorCount)

    while (([DateTime]::UtcNow - $start) -lt $duration) {
        Start-Sleep -Seconds $SampleSeconds
        $process.Refresh()
        if ($process.HasExited) { throw "Honey exited during soak test." }

        $now = [DateTime]::UtcNow
        $cpu = $process.TotalProcessorTime
        $wallSeconds = ($now - $previousAt).TotalSeconds
        $cpuPercent = (($cpu - $previousCpu).TotalSeconds / $wallSeconds / $logicalProcessors) * 100
        $samples.Add([pscustomobject]@{
            TimestampUtc = $now.ToString("O")
            ElapsedSeconds = [Math]::Round(($now - $start).TotalSeconds, 3)
            CpuPercent = [Math]::Round($cpuPercent, 3)
            WorkingSetMb = [Math]::Round($process.WorkingSet64 / 1MB, 3)
        })
        $previousAt = $now
        $previousCpu = $cpu
    }

    $reportDirectory = Split-Path -Parent $ReportPath
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    $samples | Export-Csv -LiteralPath $ReportPath -NoTypeInformation -Encoding UTF8

    $result = Get-SoakAcceptance -Samples $samples
    Write-Host ("Samples: {0}; average CPU: {1:N3}%; stable working set: {2:N3} MB; growth: {3:N3} MB" -f
        $result.SampleCount,
        $result.AverageCpuPercent,
        $result.StableWorkingSetMb,
        $result.WorkingSetGrowthMb)
    Write-Host ("Report: {0}" -f $ReportPath)
    if (-not $result.Passed) {
        throw "Soak acceptance thresholds were not met."
    }
}
finally {
    if ($null -ne $process) {
        try { Stop-SoakProcess } catch { Write-Warning $_ }
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
