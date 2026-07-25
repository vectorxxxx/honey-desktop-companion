$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\..\scripts\publish-safety.ps1")

function Assert-Throws([scriptblock]$Action, [string]$Message) {
    try { & $Action } catch { return }
    throw $Message
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("honey-publish-test-" + [Guid]::NewGuid().ToString("N"))
$external = Join-Path ([IO.Path]::GetTempPath()) ("external-honey-" + [Guid]::NewGuid().ToString("N"))
$junction = $null
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts") | Out-Null
    $safeTarget = Join-Path $root "artifacts\win-x64"

    foreach ($dangerous in @(
        $root,
        (Join-Path $root "artifacts"),
        [IO.Path]::GetPathRoot($root),
        $env:USERPROFILE,
        [Environment]::GetFolderPath("ApplicationData"),
        (Join-Path ([Environment]::GetFolderPath("ApplicationData")) "Honey"),
        $env:WINDIR,
        $external
    )) {
        Assert-Throws {
            Resolve-SafePublishOutput -RepositoryRoot $root -Output $dangerous
        } "危险路径必须被拒绝：$dangerous"
    }

    New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts\unowned") | Out-Null
    Set-Content -LiteralPath (Join-Path $root "artifacts\unowned\foreign.txt") -Value "foreign"
    Assert-Throws {
        Initialize-PublishOwnership -RepositoryRoot $root -Output "artifacts/unowned"
    } "不得接管未标记的非空目录。"

    $owned = Initialize-PublishOwnership -RepositoryRoot $root -Output $safeTarget
    New-Item -ItemType Directory -Force -Path $owned | Out-Null
    Set-Content -LiteralPath (Join-Path $owned "old.bin") -Value "old"
    $stage = New-PublishStagingDirectory -RepositoryRoot $root
    Set-Content -LiteralPath (Join-Path $stage "Honey.exe") -Value "new"
    $installed = Install-OwnedPublishStage -RepositoryRoot $root -Output $owned -Stage $stage
    if (@(Get-ChildItem -LiteralPath $installed -Force).Count -ne 1 -or
        -not (Test-Path -LiteralPath (Join-Path $installed "Honey.exe"))) {
        throw "仅受控目标应被精确替换。"
    }

    $marker = Get-PublishOwnershipMarkerPath -RepositoryRoot $root -Output $owned
    $wrong = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $wrong.CanonicalTarget = Join-Path $root "artifacts\other"
    $wrong | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding UTF8
    Assert-Throws {
        Assert-PublishOwnership -RepositoryRoot $root -Output $owned
    } "绑定错误的标记必须被拒绝。"

    $junctionSource = Join-Path $root "junction-source"
    $junction = Join-Path $root "artifacts\junction"
    New-Item -ItemType Directory -Path $junctionSource | Out-Null
    New-Item -ItemType Junction -Path $junction -Target $junctionSource | Out-Null
    Assert-Throws {
        Resolve-SafePublishOutput -RepositoryRoot $root -Output (Join-Path $junction "child")
    } "联接点路径必须被拒绝。"

    Write-Host "Publish safety tests passed: dangerous paths, ownership, staging and reparse checks"
}
finally {
    if ($junction -and (Test-Path -LiteralPath $junction)) {
        Remove-Item -LiteralPath $junction -Force
    }
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
    if (Test-Path -LiteralPath $external) {
        Remove-Item -LiteralPath $external -Recurse -Force
    }
}
