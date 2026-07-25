$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\..\scripts\publish-safety.ps1")

function Assert-Throws([scriptblock]$Action, [string]$Message) {
    try {
        & $Action
    }
    catch {
        return
    }
    throw $Message
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("honey-publish-test-" + [Guid]::NewGuid().ToString("N"))
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts\win-x64\old\nested") | Out-Null
    Set-Content -LiteralPath (Join-Path $root "artifacts\win-x64\old\nested\stale.dll") -Value "old"

    $output = Reset-SafePublishOutput -RepositoryRoot $root -Output "artifacts/win-x64"

    if (@(Get-ChildItem -LiteralPath $output -Force).Count -ne 0) {
        throw "Reset must remove nested stale output."
    }
    Assert-Throws { Resolve-SafePublishOutput -RepositoryRoot $root -Output $root } "Repository root must be rejected."
    Assert-Throws { Resolve-SafePublishOutput -RepositoryRoot $root -Output ([IO.Path]::GetPathRoot($root)) } "Drive root must be rejected."
    Assert-Throws {
        Resolve-SafePublishOutput -RepositoryRoot $root -Output (Join-Path ([IO.Path]::GetTempPath()) "external-honey")
    } "External output must require explicit opt-in."

    $external = Resolve-SafePublishOutput `
        -RepositoryRoot $root `
        -Output (Join-Path ([IO.Path]::GetTempPath()) "external-honey") `
        -AllowExternalOutput
    if (-not [IO.Path]::IsPathRooted($external)) {
        throw "Resolved output must be absolute."
    }
    Write-Host "Publish safety tests passed: 5 groups"
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
