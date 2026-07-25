$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\..\scripts\soak-lifecycle.ps1")

function Assert-Throws([scriptblock]$Action, [string]$Message) {
    try { & $Action } catch { return }
    throw $Message
}

Assert-Throws {
    Assert-SoakLifecycleOutcome -FinalIdentityAlive:$false -ShutdownExitCode 0 -ResidualExactInstance:$false
} "最终采样后崩溃必须失败。"
Assert-Throws {
    Assert-SoakLifecycleOutcome -FinalIdentityAlive:$true -ShutdownExitCode 3 -ResidualExactInstance:$false
} "关停命令失败必须失败。"
Assert-Throws {
    Assert-SoakLifecycleOutcome -FinalIdentityAlive:$true -ShutdownExitCode 0 -ResidualExactInstance:$true
} "残留隔离实例必须失败。"
Assert-SoakLifecycleOutcome -FinalIdentityAlive:$true -ShutdownExitCode 0 -ResidualExactInstance:$false

$expected = @{
    ExpectedProcessId = 42
    ExpectedStartTimeUtc = [datetime]"2026-07-26T00:00:00Z"
    ExpectedExePath = "C:\Honey\Honey.exe"
    ExpectedInstanceId = "instanceA"
    ActualProcessId = 42
    ActualStartTimeUtc = [datetime]"2026-07-26T00:00:00Z"
    ActualExePath = "c:\honey\HONEY.exe"
    ActualCommandLine = '"C:\Honey\Honey.exe" --instance-id instanceA'
}
if (-not (Test-HoneyIdentitySnapshot @expected)) {
    throw "完全相同的四重身份应通过。"
}
foreach ($mutation in @(
    @{ ActualProcessId = 43 },
    @{ ActualStartTimeUtc = [datetime]"2026-07-26T00:00:01Z" },
    @{ ActualCommandLine = '"C:\Honey\Honey.exe" --instance-id instanceB' }
)) {
    $probe = $expected.Clone()
    foreach ($key in $mutation.Keys) { $probe[$key] = $mutation[$key] }
    if (Test-HoneyIdentitySnapshot @probe) {
        throw "PID 复用、启动时间变化或不同 InstanceId 必须拒绝停止。"
    }
}
Write-Host "Soak lifecycle tests passed: lifecycle failures and four-part identity guard"
