param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [string]$DataRoot,
    [int]$TimeoutSeconds = 15,
    [int]$StabilitySeconds = 12,
    [switch]$KeepData
)

$ErrorActionPreference = "Stop"
$executable = (Resolve-Path -LiteralPath $ExePath -ErrorAction Stop).Path
$ownsDataRoot = [string]::IsNullOrWhiteSpace($DataRoot)
if ($ownsDataRoot) {
    $DataRoot = Join-Path ([IO.Path]::GetTempPath()) ("honey-smoke-" + [Guid]::NewGuid().ToString("N"))
}
$DataRoot = [IO.Path]::GetFullPath($DataRoot)
$primary = $null

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
        -ArgumentList @($Command, "--data-root", "`"$DataRoot`"") `
        -PassThru
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
        if ($null -ne $candidate -and
            [string]::Equals($candidate.Path, $executable, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $primary.Id -Force
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
    Wait-ProcessState -Id $primary.Id -ShouldExist $true
    Wait-HoneyStable -Process $primary

    $secondary = Start-Honey "--background"
    if (-not $secondary.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Secondary instance did not exit."
    }
    if ($primary.HasExited) { throw "Primary instance was replaced by the secondary instance." }

    $show = Start-Honey "--show"
    if (-not $show.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Show command did not return."
    }
    if ($primary.HasExited) { throw "Show command terminated the primary instance." }

    Stop-IsolatedHoney
    $primary = $null

    $database = Join-Path $DataRoot "honey.db"
    if (-not (Test-Path -LiteralPath $database -PathType Leaf)) {
        throw "SQLite archive was not created."
    }
    $stream = [IO.File]::Open($database, "Open", "Read", "ReadWrite")
    try {
        $header = New-Object byte[] 16
        if ($stream.Read($header, 0, $header.Length) -ne 16) {
            throw "SQLite archive is too short."
        }
        $signature = [Text.Encoding]::ASCII.GetString($header)
        if ($signature -ne "SQLite format 3`0") {
            throw "SQLite archive header is invalid."
        }
    }
    finally {
        $stream.Dispose()
    }

    $primary = Start-Honey "--background"
    Wait-ProcessState -Id $primary.Id -ShouldExist $true
    Start-Sleep -Milliseconds 800
    if ($primary.HasExited) { throw "Restarted instance exited unexpectedly." }
    Stop-IsolatedHoney
    $primary = $null

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
