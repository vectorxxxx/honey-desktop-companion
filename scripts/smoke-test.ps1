param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [string]$DataRoot,
    [string]$InstanceId = ([Guid]::NewGuid().ToString("N")),
    [int]$TimeoutSeconds = 15,
    [int]$StabilitySeconds = 12,
    [switch]$KeepData
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "soak-lifecycle.ps1")
$executable = (Resolve-Path -LiteralPath $ExePath -ErrorAction Stop).Path
$ownsDataRoot = [string]::IsNullOrWhiteSpace($DataRoot)
if ($ownsDataRoot) {
    $DataRoot = Join-Path ([IO.Path]::GetTempPath()) ("honey-smoke-" + [Guid]::NewGuid().ToString("N"))
}
$DataRoot = [IO.Path]::GetFullPath($DataRoot)
$primary = $null
$primaryStartTimeUtc = $null

function Wait-ProcessState {
    param(
        [int]$Id,
        [bool]$ShouldExist
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $exists = $null -ne (Get-Process -Id $Id -ErrorAction SilentlyContinue)
        if ($exists -eq $ShouldExist) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Process $Id did not reach expected state: $ShouldExist"
}

function Start-Honey {
    param([string]$Command)
    Start-Process -FilePath $executable `
        -ArgumentList @(
            $Command,
            "--data-root",
            "`"$DataRoot`"",
            "--instance-id",
            $InstanceId) `
        -PassThru
}

function Get-IsolatedHoneyProcesses {
    @(Get-CimInstance Win32_Process -Filter "Name = 'Honey.exe'" |
        Where-Object {
            [string]::Equals(
                $_.ExecutablePath,
                $executable,
                [StringComparison]::OrdinalIgnoreCase) -and
            $_.CommandLine -match [Regex]::Escape($InstanceId)
        })
}

function Assert-IsolatedCount {
    param([int]$Expected)
    $matches = @(Get-IsolatedHoneyProcesses)
    if ($matches.Count -ne $Expected) {
        throw "Expected $Expected isolated Honey process(es), found $($matches.Count)."
    }
    return $matches
}

function Wait-HoneyStable {
    param([Diagnostics.Process]$Process)
    $deadline = [DateTime]::UtcNow.AddSeconds($StabilitySeconds)
    do {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "Background instance exited during the stability window."
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
}

function Stop-IsolatedHoney {
    if ($null -eq $primary) { return }
    $running = Get-Process -Id $primary.Id -ErrorAction SilentlyContinue
    if ($null -eq $running) { return }

    $stopper = Start-Honey "--shutdown"
    $stopper.WaitForExit($TimeoutSeconds * 1000) | Out-Null
    try {
        Wait-ProcessState -Id $primary.Id -ShouldExist $false
    }
    catch {
        $candidate = Get-Process -Id $primary.Id -ErrorAction SilentlyContinue
        if ($null -ne $candidate) {
            $stopped = Stop-ExactHoneyProcess `
                -ProcessId $primary.Id `
                -StartTimeUtc $primaryStartTimeUtc `
                -ExePath $executable `
                -InstanceId $InstanceId
            if (-not $stopped) {
                throw "拒绝停止身份不匹配的进程；可能发生 PID 复用。"
            }
        }
        throw
    }
}

try {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $noInstanceStopper = Start-Honey "--shutdown"
    if (-not $noInstanceStopper.WaitForExit(3000)) {
        throw "Shutdown without an instance did not return quickly."
    }
    $watch.Stop()
    if (Test-Path -LiteralPath $DataRoot) {
        throw "Shutdown without an instance initialized the data directory."
    }

    $primary = Start-Honey "--background"
    $primaryStartTimeUtc = $primary.StartTime.ToUniversalTime()
    Wait-ProcessState -Id $primary.Id -ShouldExist $true
    $earlyShow = Start-Honey "--show"
    if (-not $earlyShow.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Show command during primary initialization timed out."
    }
    if ($earlyShow.ExitCode -ne 0) {
        throw "Show command during primary initialization was not acknowledged."
    }
    Wait-HoneyStable -Process $primary
    $matches = @(Assert-IsolatedCount -Expected 1)
    if ($matches[0].ProcessId -ne $primary.Id) {
        throw "Isolated process identity does not match the launched primary."
    }

    $secondary = Start-Honey "--background"
    if (-not $secondary.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Secondary instance did not exit."
    }
    if ($primary.HasExited) { throw "Primary instance was replaced by the secondary instance." }
    $null = Assert-IsolatedCount -Expected 1

    $show = Start-Honey "--show"
    if (-not $show.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Show command did not return."
    }
    if ($primary.HasExited) { throw "Show command terminated the primary instance." }
    $null = Assert-IsolatedCount -Expected 1

    Stop-IsolatedHoney
    $primary = $null
    $primaryStartTimeUtc = $null
    $null = Assert-IsolatedCount -Expected 0

    $database = Join-Path $DataRoot "honey.db"
    if (-not (Test-Path -LiteralPath $database -PathType Leaf)) {
        throw "SQLite archive was not created."
    }
    $verifier = Start-Honey "--verify-data"
    if (-not $verifier.WaitForExit($TimeoutSeconds * 1000)) {
        throw "SQLite deep verification timed out."
    }
    if ($verifier.ExitCode -ne 0) {
        throw "SQLite deep verification failed with exit code $($verifier.ExitCode)."
    }

    $corruptRoot = Join-Path $DataRoot "corrupt-probe"
    New-Item -ItemType Directory -Force -Path $corruptRoot | Out-Null
    [IO.File]::WriteAllBytes(
        (Join-Path $corruptRoot "honey.db"),
        [Text.Encoding]::ASCII.GetBytes("not-a-sqlite-database"))
    $corruptVerifier = Start-Process -FilePath $executable `
        -ArgumentList @(
            "--verify-data",
            "--data-root",
            "`"$corruptRoot`"",
            "--instance-id",
            $InstanceId) `
        -PassThru
    if (-not $corruptVerifier.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Corrupt SQLite verification did not return."
    }
    if ($corruptVerifier.ExitCode -eq 0) {
        throw "Corrupt SQLite archive was incorrectly accepted."
    }
    Remove-Item -LiteralPath $corruptRoot -Recurse -Force

    $primary = Start-Honey "--background"
    $primaryStartTimeUtc = $primary.StartTime.ToUniversalTime()
    Wait-ProcessState -Id $primary.Id -ShouldExist $true
    Start-Sleep -Milliseconds 800
    if ($primary.HasExited) { throw "Restarted instance exited unexpectedly." }
    Stop-IsolatedHoney
    $primary = $null
    $primaryStartTimeUtc = $null
    $null = Assert-IsolatedCount -Expected 0

    Write-Host ("Smoke test passed. No-instance shutdown: {0:N0} ms" -f $watch.Elapsed.TotalMilliseconds)
    Write-Host ("Data root: {0}" -f $DataRoot)
}
finally {
    if ($null -ne $primary) {
        try { Stop-IsolatedHoney } catch { Write-Warning $_ }
    }
    if ($ownsDataRoot -and -not $KeepData -and (Test-Path -LiteralPath $DataRoot)) {
        $resolved = [IO.Path]::GetFullPath($DataRoot)
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $resolved).StartsWith("honey-smoke-")) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
