function Resolve-SafePublishOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string]$Output,
        [switch]$AllowExternalOutput
    )

    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $resolved = if ([IO.Path]::IsPathRooted($Output)) {
        [IO.Path]::GetFullPath($Output)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repository $Output))
    }
    $resolved = $resolved.TrimEnd('\', '/')
    $root = [IO.Path]::GetPathRoot($resolved).TrimEnd('\', '/')
    if ([string]::Equals($resolved, $root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "A drive root cannot be used as publish output."
    }
    if ([string]::Equals($resolved, $repository, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The repository root cannot be used as publish output."
    }

    $artifacts = (Join-Path $repository "artifacts").TrimEnd('\', '/')
    $insideArtifacts =
        [string]::Equals($resolved, $artifacts, [StringComparison]::OrdinalIgnoreCase) -or
        $resolved.StartsWith(
            $artifacts + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
    if (-not $insideArtifacts -and -not $AllowExternalOutput) {
        throw "External publish output requires -AllowExternalOutput."
    }

    $cursor = $resolved
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Publish output path cannot pass through a reparse point."
            }
        }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or
            [string]::Equals($parent, $cursor, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $cursor = $parent
    }
    return $resolved
}

function Reset-SafePublishOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string]$Output,
        [switch]$AllowExternalOutput
    )

    $resolved = Resolve-SafePublishOutput `
        -RepositoryRoot $RepositoryRoot `
        -Output $Output `
        -AllowExternalOutput:$AllowExternalOutput
    if (Test-Path -LiteralPath $resolved) {
        $reparsePoints = @(Get-ChildItem -LiteralPath $resolved -Recurse -Force |
            Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
        $rootItem = Get-Item -LiteralPath $resolved -Force
        if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $reparsePoints.Count -gt 0) {
            throw "Publish output containing reparse points cannot be reset."
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolved | Out-Null
    return $resolved
}
