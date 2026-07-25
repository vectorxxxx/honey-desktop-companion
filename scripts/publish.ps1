param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts/win-x64",
    [string]$DotnetPath
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

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
    if (Test-Path -LiteralPath $repositorySdk -PathType Leaf) {
        return $repositorySdk
    }

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
$outputPath = if ([IO.Path]::IsPathRooted($Output)) {
    [IO.Path]::GetFullPath($Output)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repo $Output))
}

Push-Location $repo
try {
    & $dotnet restore Honey.slnx --source "https://api.nuget.org/v3/index.json"
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }

    & $dotnet test Honey.slnx -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }

    & $dotnet restore src/Honey.Desktop/Honey.Desktop.csproj `
        -r win-x64 `
        -p:PublishReadyToRun=true `
        -p:PublishSingleFile=true `
        --source "https://api.nuget.org/v3/index.json"
    if ($LASTEXITCODE -ne 0) { throw "Publish restore failed: $LASTEXITCODE" }

    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    & $dotnet publish src/Honey.Desktop/Honey.Desktop.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        --no-restore `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=true `
        -p:DebugType=embedded `
        -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

    $executable = Join-Path $outputPath "Honey.exe"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Publish did not produce Honey.exe."
    }

    Get-ChildItem -LiteralPath $outputPath -File -Filter "*.pdb" |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    $unexpected = @(Get-ChildItem -LiteralPath $outputPath -File |
        Where-Object Name -ne "Honey.exe")
    if ($unexpected.Count -gt 0) {
        throw "Single-file output contains unexpected files: $($unexpected.Name -join ', ')"
    }

    $item = Get-Item -LiteralPath $executable
    $hash = Get-FileHash -LiteralPath $executable -Algorithm SHA256
    Write-Host ("Published: {0}" -f $item.FullName)
    Write-Host ("Bytes: {0}" -f $item.Length)
    Write-Host ("SHA256: {0}" -f $hash.Hash)
}
finally {
    Pop-Location
    $env:DOTNET_ROOT = $previousDotnetRoot
    $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse
}
