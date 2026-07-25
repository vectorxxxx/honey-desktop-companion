$script:PublishOwnerMagic = "Honey.PublishOwner"
$script:PublishOwnerVersion = 1

function Get-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Test-IsStrictDescendant {
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )

    $parentPath = Get-NormalizedPath $Parent
    $childPath = Get-NormalizedPath $Child
    return $childPath.StartsWith(
        $parentPath + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$StopAt
    )

    $cursor = Get-NormalizedPath $Path
    $stop = Get-NormalizedPath $StopAt
    while ($true) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "发布路径不得包含重解析点：$cursor"
            }
        }
        if ([string]::Equals($cursor, $stop, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or
            [string]::Equals($parent, $cursor, [StringComparison]::OrdinalIgnoreCase)) {
            throw "发布路径不在预期仓库中。"
        }
        $cursor = $parent
    }
}

function Resolve-SafePublishOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Output
    )

    $repository = Get-NormalizedPath $RepositoryRoot
    $artifacts = Get-NormalizedPath (Join-Path $repository "artifacts")
    $resolved = if ([IO.Path]::IsPathRooted($Output)) {
        Get-NormalizedPath $Output
    }
    else {
        Get-NormalizedPath (Join-Path $repository $Output)
    }

    if (-not (Test-IsStrictDescendant -Parent $artifacts -Child $resolved)) {
        throw "发布输出必须是仓库 artifacts 目录的严格子目录。"
    }
    $reservedOwners = Join-Path $artifacts ".honey-publish-owners"
    $reservedStaging = Join-Path $artifacts ".honey-staging"
    if ([string]::Equals($resolved, (Get-NormalizedPath $reservedOwners), [StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($resolved, (Get-NormalizedPath $reservedStaging), [StringComparison]::OrdinalIgnoreCase) -or
        (Test-IsStrictDescendant -Parent $reservedOwners -Child $resolved) -or
        (Test-IsStrictDescendant -Parent $reservedStaging -Child $resolved)) {
        throw "发布输出不得使用内部控制目录。"
    }
    Assert-NoReparsePath -Path $resolved -StopAt $repository
    return $resolved
}

function Get-PublishOwnershipMarkerPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Output
    )

    $repository = Get-NormalizedPath $RepositoryRoot
    $target = Resolve-SafePublishOutput -RepositoryRoot $repository -Output $Output
    $bytes = [Text.Encoding]::UTF8.GetBytes($target.ToUpperInvariant())
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
    return Join-Path $repository "artifacts\.honey-publish-owners\$hash.json"
}

function Test-PublishTargetNonEmpty {
    param([Parameter(Mandatory = $true)][string]$Target)

    return (Test-Path -LiteralPath $Target) -and
        @(Get-ChildItem -LiteralPath $Target -Force -ErrorAction Stop).Count -gt 0
}

function Assert-PublishOwnership {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Output
    )

    $target = Resolve-SafePublishOutput -RepositoryRoot $RepositoryRoot -Output $Output
    $markerPath = Get-PublishOwnershipMarkerPath -RepositoryRoot $RepositoryRoot -Output $target
    Assert-NoReparsePath -Path $markerPath -StopAt (Get-NormalizedPath $RepositoryRoot)
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        throw "发布目标缺少可信所有权标记：$target"
    }
    try {
        $marker = Get-Content -LiteralPath $markerPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "发布目标所有权标记损坏：$markerPath"
    }
    if ($marker.Magic -ne $script:PublishOwnerMagic -or
        [int]$marker.Version -ne $script:PublishOwnerVersion -or
        -not [string]::Equals(
            (Get-NormalizedPath ([string]$marker.CanonicalTarget)),
            $target,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "发布目标所有权标记不匹配：$target"
    }
    Assert-NoReparsePath -Path $target -StopAt (Get-NormalizedPath $RepositoryRoot)
    return $target
}

function Initialize-PublishOwnership {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Output
    )

    $target = Resolve-SafePublishOutput -RepositoryRoot $RepositoryRoot -Output $Output
    $markerPath = Get-PublishOwnershipMarkerPath -RepositoryRoot $RepositoryRoot -Output $target
    if (Test-Path -LiteralPath $markerPath) {
        [void](Assert-PublishOwnership -RepositoryRoot $RepositoryRoot -Output $target)
        return $target
    }
    if (Test-PublishTargetNonEmpty $target) {
        throw "拒绝接管未标记的非空发布目标：$target"
    }

    $markerDirectory = Split-Path -Parent $markerPath
    New-Item -ItemType Directory -Force -Path $markerDirectory | Out-Null
    Assert-NoReparsePath -Path $markerDirectory -StopAt (Get-NormalizedPath $RepositoryRoot)
    $payload = [ordered]@{
        Magic = $script:PublishOwnerMagic
        Version = $script:PublishOwnerVersion
        CanonicalTarget = $target
    } | ConvertTo-Json
    $temporaryMarker = "$markerPath.$([Guid]::NewGuid().ToString('N')).tmp"
    Set-Content -LiteralPath $temporaryMarker -Value $payload -Encoding UTF8 -NoNewline
    Move-Item -LiteralPath $temporaryMarker -Destination $markerPath
    return Assert-PublishOwnership -RepositoryRoot $RepositoryRoot -Output $target
}

function New-PublishStagingDirectory {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $repository = Get-NormalizedPath $RepositoryRoot
    $stagingRoot = Join-Path $repository "artifacts\.honey-staging"
    New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
    Assert-NoReparsePath -Path $stagingRoot -StopAt $repository
    $stage = Join-Path $stagingRoot ([Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $stage | Out-Null
    return $stage
}

function Assert-SingleExecutableStage {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Stage
    )

    $repository = Get-NormalizedPath $RepositoryRoot
    $stagePath = Get-NormalizedPath $Stage
    $stagingRoot = Get-NormalizedPath (Join-Path $repository "artifacts\.honey-staging")
    if (-not (Test-IsStrictDescendant -Parent $stagingRoot -Child $stagePath)) {
        throw "暂存目录不受发布流程控制。"
    }
    Assert-NoReparsePath -Path $stagePath -StopAt $repository
    $entries = @(Get-ChildItem -LiteralPath $stagePath -Recurse -Force)
    $executable = Join-Path $stagePath "Honey.exe"
    if ($entries.Count -ne 1 -or
        -not (Test-Path -LiteralPath $executable -PathType Leaf) -or
        -not [string]::Equals($entries[0].FullName, $executable, [StringComparison]::OrdinalIgnoreCase)) {
        throw "暂存目录必须且只能包含根目录 Honey.exe。"
    }
    return $stagePath
}

function Install-OwnedPublishStage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Output,
        [Parameter(Mandatory = $true)][string]$Stage
    )

    $target = Assert-PublishOwnership -RepositoryRoot $RepositoryRoot -Output $Output
    $stagePath = Assert-SingleExecutableStage -RepositoryRoot $RepositoryRoot -Stage $Stage

    # 在任何破坏性操作前再次验证目标、标记和路径，缩小检查与使用之间的窗口。
    $target = Assert-PublishOwnership -RepositoryRoot $RepositoryRoot -Output $target
    [void](Assert-SingleExecutableStage -RepositoryRoot $RepositoryRoot -Stage $stagePath)
    if (Test-Path -LiteralPath $target) {
        $nestedReparse = @(Get-ChildItem -LiteralPath $target -Recurse -Force |
            Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
        if ($nestedReparse.Count -gt 0) {
            throw "发布目标内含重解析点，拒绝替换。"
        }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    Move-Item -LiteralPath $stagePath -Destination $target
    return Assert-PublishOwnership -RepositoryRoot $RepositoryRoot -Output $target
}
