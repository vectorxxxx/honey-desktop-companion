param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts/win-x64",
    [string]$FrameworkDependentOutput = "artifacts/win-x64-framework-dependent",
    [string]$DotnetPath
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "publish-safety.ps1")

function Resolve-Dotnet {
    param([string]$ExplicitPath)

    if ($ExplicitPath) {
        $resolved = (Resolve-Path -LiteralPath $ExplicitPath -ErrorAction Stop).Path
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "DotnetPath must point to dotnet.exe."
        }
        return $resolved
    }
    $repositorySdk = Join-Path $repo ".dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $repositorySdk -PathType Leaf) { return $repositorySdk }
    $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "dotnet.exe was not found. Supply -DotnetPath or place the SDK in .dotnet."
    }
    return $command.Source
}

$dotnet = Resolve-Dotnet $DotnetPath
$previousDotnetRoot = $env:DOTNET_ROOT
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:MSBUILDDISABLENODEREUSE = "1"
$outputPath = Resolve-SafePublishOutput -RepositoryRoot $repo -Output $Output
$frameworkDependentOutputPath = Resolve-SafePublishOutput `
    -RepositoryRoot $repo -Output $FrameworkDependentOutput
$stagePath = $null

Push-Location $repo
try {
    & $dotnet restore Honey.slnx --source "https://api.nuget.org/v3/index.json"
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
    & $dotnet test Honey.slnx -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
    & $dotnet restore src/Honey.Desktop/Honey.Desktop.csproj `
        -r win-x64 -p:PublishReadyToRun=false -p:PublishSingleFile=true `
        --source "https://api.nuget.org/v3/index.json"
    if ($LASTEXITCODE -ne 0) { throw "Publish restore failed: $LASTEXITCODE" }

    $outputPath = Initialize-PublishOwnership -RepositoryRoot $repo -Output $outputPath
    $stagePath = New-PublishStagingDirectory -RepositoryRoot $repo
    & $dotnet publish src/Honey.Desktop/Honey.Desktop.csproj `
        -c $Configuration -r win-x64 --self-contained true --no-restore `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=false `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded `
        -o $stagePath
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

    Get-ChildItem -LiteralPath $stagePath -Recurse -File -Filter "*.pdb" |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    Get-ChildItem -LiteralPath $stagePath -Recurse -Directory |
        Sort-Object FullName -Descending |
        Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Force).Count -eq 0 } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    [void](Assert-SingleExecutableStage -RepositoryRoot $repo -Stage $stagePath)
    $outputPath = Install-OwnedPublishStage `
        -RepositoryRoot $repo -Output $outputPath -Stage $stagePath
    $stagePath = $null

    $executable = Join-Path $outputPath "Honey.exe"
    $item = Get-Item -LiteralPath $executable
    $hash = Get-FileHash -LiteralPath $executable -Algorithm SHA256
    Write-Host ("Published: {0}" -f $item.FullName)
    Write-Host ("Bytes: {0}" -f $item.Length)
    Write-Host ("SHA256: {0}" -f $hash.Hash)

    $frameworkDependentOutputPath = Initialize-PublishOwnership `
        -RepositoryRoot $repo -Output $frameworkDependentOutputPath
    $stagePath = New-PublishStagingDirectory -RepositoryRoot $repo
    & $dotnet publish src/Honey.Desktop/Honey.Desktop.csproj `
        -c $Configuration -r win-x64 --self-contained false --no-restore `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=false `
        -p:EnableCompressionInSingleFile=false `
        -p:DebugType=embedded `
        -o $stagePath
    if ($LASTEXITCODE -ne 0) {
        throw "Framework-dependent publish failed: $LASTEXITCODE"
    }

    Get-ChildItem -LiteralPath $stagePath -Recurse -File -Filter "*.pdb" |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    Get-ChildItem -LiteralPath $stagePath -Recurse -Directory |
        Sort-Object FullName -Descending |
        Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Force).Count -eq 0 } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    [void](Assert-SingleExecutableStage -RepositoryRoot $repo -Stage $stagePath)
    $frameworkDependentOutputPath = Install-OwnedPublishStage `
        -RepositoryRoot $repo -Output $frameworkDependentOutputPath -Stage $stagePath
    $stagePath = $null

    $frameworkDependentExecutable = Join-Path $frameworkDependentOutputPath "Honey.exe"
    $frameworkDependentItem = Get-Item -LiteralPath $frameworkDependentExecutable
    $frameworkDependentHash = Get-FileHash `
        -LiteralPath $frameworkDependentExecutable -Algorithm SHA256
    Write-Host ("Published (requires .NET 10 Desktop Runtime): {0}" `
        -f $frameworkDependentItem.FullName)
    Write-Host ("Bytes: {0}" -f $frameworkDependentItem.Length)
    Write-Host ("SHA256: {0}" -f $frameworkDependentHash.Hash)
}
finally {
    if ($stagePath -and (Test-Path -LiteralPath $stagePath)) {
        $stagingRoot = Get-NormalizedPath (Join-Path $repo "artifacts\.honey-staging")
        $resolvedStage = Get-NormalizedPath $stagePath
        if (Test-IsStrictDescendant -Parent $stagingRoot -Child $resolvedStage) {
            Assert-NoReparsePath -Path $resolvedStage -StopAt (Get-NormalizedPath $repo)
            $stageReparse = @(Get-ChildItem -LiteralPath $resolvedStage -Recurse -Force |
                Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
            if ($stageReparse.Count -gt 0) {
                throw "暂存目录含重解析点，拒绝递归清理。"
            }
            Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        }
    }
    Pop-Location
    $env:DOTNET_ROOT = $previousDotnetRoot
    $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse
}
