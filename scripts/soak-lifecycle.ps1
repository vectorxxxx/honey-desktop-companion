function Test-HoneyIdentitySnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ExpectedProcessId,
        [Parameter(Mandatory = $true)][datetime]$ExpectedStartTimeUtc,
        [Parameter(Mandatory = $true)][string]$ExpectedExePath,
        [Parameter(Mandatory = $true)][string]$ExpectedInstanceId,
        [Parameter(Mandatory = $true)][int]$ActualProcessId,
        [Parameter(Mandatory = $true)][datetime]$ActualStartTimeUtc,
        [Parameter(Mandatory = $true)][string]$ActualExePath,
        [Parameter(Mandatory = $true)][string]$ActualCommandLine
    )

    return $ActualProcessId -eq $ExpectedProcessId -and
        $ActualStartTimeUtc -eq $ExpectedStartTimeUtc -and
        [string]::Equals(
            [IO.Path]::GetFullPath($ActualExePath),
            [IO.Path]::GetFullPath($ExpectedExePath),
            [StringComparison]::OrdinalIgnoreCase) -and
        $ActualCommandLine.IndexOf($ExpectedInstanceId, [StringComparison]::Ordinal) -ge 0
}

function Test-HoneyProcessIdentity {
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
        $actualStart = $process.StartTime.ToUniversalTime()
        $actualPath = $process.Path
    }
    catch { return $false }
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
    if ($null -eq $cim) { return $false }
    return Test-HoneyIdentitySnapshot `
        -ExpectedProcessId $ProcessId `
        -ExpectedStartTimeUtc $StartTimeUtc `
        -ExpectedExePath $ExePath `
        -ExpectedInstanceId $InstanceId `
        -ActualProcessId ([int]$cim.ProcessId) `
        -ActualStartTimeUtc $actualStart `
        -ActualExePath ([string]$cim.ExecutablePath) `
        -ActualCommandLine ([string]$cim.CommandLine)
}

function Test-SoakIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][datetime]$StartTimeUtc,
        [Parameter(Mandatory = $true)][string]$ExePath,
        [Parameter(Mandatory = $true)][string]$InstanceId
    )
    return Test-HoneyProcessIdentity @PSBoundParameters
}

function Stop-ExactHoneyProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][datetime]$StartTimeUtc,
        [Parameter(Mandatory = $true)][string]$ExePath,
        [Parameter(Mandatory = $true)][string]$InstanceId
    )

    if (-not (Test-HoneyProcessIdentity @PSBoundParameters)) {
        return $false
    }
    Stop-Process -Id $ProcessId -Force
    return $true
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
