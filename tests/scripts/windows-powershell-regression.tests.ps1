$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSEdition -ne "Desktop" -or $PSVersionTable.PSVersion.Major -ne 5) {
    throw "该回归必须由 Windows PowerShell 5.1 powershell.exe 执行。"
}

$tests = @(
    "publish-safety.tests.ps1",
    "soak-metrics.tests.ps1",
    "soak-lifecycle.tests.ps1"
)
foreach ($test in $tests) {
    $path = Join-Path $PSScriptRoot $test
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $path
    if ($LASTEXITCODE -ne 0) {
        throw "Windows PowerShell 回归失败：$test，退出码 $LASTEXITCODE。"
    }
}
Write-Host "Windows PowerShell 5.1 regression passed: $($tests.Count) script suites"
