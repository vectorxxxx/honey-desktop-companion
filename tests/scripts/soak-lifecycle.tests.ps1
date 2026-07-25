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
Write-Host "Soak lifecycle tests passed: late crash, shutdown failure and residual checks"
