param(
    [string]$Version = "0.3.0",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$outputsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "outputs"))
$packageName = "StructuralToolbox.DrawingTranslation_preview10_AutoCAD2020"
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $outputsRoot $packageName
}
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
$outputsPrefix = $outputsRoot.TrimEnd("\") + [IO.Path]::DirectorySeparatorChar
if (-not $packageRoot.StartsWith($outputsPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "The drawing translation package must remain under outputs/: $packageRoot"
}
if ([IO.Path]::GetFileName($packageRoot) -ne $packageName) {
    throw "Unexpected drawing translation package directory: $packageRoot"
}

$workBuild = Join-Path $projectRoot "work\module-builds\drawing-translation-package"
$contractsBuild = Join-Path $projectRoot "work\module-builds\translation-contracts"
& (Join-Path $PSScriptRoot "build-contracts.ps1") `
    -OutputDirectory $contractsBuild | Out-Null
$contractsPath = Join-Path $contractsBuild "StructuralToolbox.Contracts.dll"
& (Join-Path $PSScriptRoot "build-drawing-translation.ps1") `
    -OutputDirectory $workBuild -ContractsPath $contractsPath
if ($LASTEXITCODE -ne 0) { throw "Drawing translation module build failed." }
$buildPath = Join-Path $workBuild "StructuralToolbox.DrawingTranslation.dll"
if (-not (Test-Path -LiteralPath $buildPath -PathType Leaf)) {
    throw "Drawing translation build output is missing: $buildPath"
}

if (Test-Path -LiteralPath $packageRoot -PathType Container) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $buildPath -Destination $packageRoot -Force
Copy-Item -LiteralPath $contractsPath -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.DrawingTranslation\README.md") `
    -Destination (Join-Path $packageRoot "README.md") -Force

$manifest = [ordered]@{
    name = "StructuralToolbox.DrawingTranslation"
    displayName = "Drawing Translation standalone preview"
    version = "0.3.0.0"
    preview = 10
    host = "AutoCAD 2020 Win64"
    integratedIntoMainToolbox = $false
    commands = @("STPVTRANSLATESETTINGS", "STPVTRANSLATETEST",
        "STPVTRANSLATE", "STPVTRANSLATECONFIRM", "STPVTRANSLATEINFO",
        "STPVTRANSLATELOG")
    features = @("glossary", "translation-cache", "batch-progress-cancel",
        "non-xref-attributes", "main-toolbox-module")
    artifactSha256 = (Get-FileHash -LiteralPath $buildPath -Algorithm SHA256).Hash
}
$manifestText = (($manifest | ConvertTo-Json -Depth 5) -replace "`r`n?", "`n")
[IO.File]::WriteAllText((Join-Path $packageRoot "manifest.json"),
    $manifestText + "`n", (New-Object Text.UTF8Encoding($false)))

$artifactNames = @("StructuralToolbox.DrawingTranslation.dll",
    "StructuralToolbox.Contracts.dll", "README.md", "manifest.json")
$checksumLines = foreach ($name in $artifactNames) {
    $path = Join-Path $packageRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Package artifact is missing: $path"
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    "$hash  $name"
}
[IO.File]::WriteAllText((Join-Path $packageRoot "SHA256SUMS.txt"),
    ($checksumLines -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))

Write-Host "Standalone package: $packageRoot"
Get-ChildItem -LiteralPath $packageRoot -File |
    Sort-Object Name | Select-Object Name, Length, LastWriteTime
