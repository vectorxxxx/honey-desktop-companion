function Test-SoakIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][datetime]$StartTimeUtc,
        [Parameter(Mandatory = $true)][string]$ExePath,
        [Parameter(Mandatory = $true)][string]$InstanceId
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process -or $process.HasExited) { return $false }
    try {
        if ($process.StartTime.ToUniversalTime() -ne $StartTimeUtc -or
            -not [string]::Equals(
                [IO.Path]::GetFullPath($process.Path),
                [IO.Path]::GetFullPath($ExePath),
                [StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }
    catch { return $false }

    $cim = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
    return $null -ne $cim -and
        [string]::Equals(
            [IO.Path]::GetFullPath([string]$cim.ExecutablePath),
            [IO.Path]::GetFullPath($ExePath),
            [StringComparison]::OrdinalIgnoreCase) -and
        ([string]$cim.CommandLine).Contains($InstanceId, [StringComparison]::Ordinal)
}

function Assert-SoakLifecycleOutcome {
    [CmdletBinding()]
    param(
        [bool]$FinalIdentityAlive,
        [int]$ShutdownExitCode,
        [bool]$ResidualExactInstance
    )

    if (-not $FinalIdentityAlive) {
        throw "Honey 在最终采样确认前退出或身份发生变化。"
    }
    if ($ShutdownExitCode -ne 0) {
        throw "Honey 关停命令失败，退出码：$ShutdownExitCode。"
    }
    if ($ResidualExactInstance) {
        throw "Honey 关停后仍残留本次隔离实例。"
    }
}
