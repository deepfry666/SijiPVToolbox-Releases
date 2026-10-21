param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$OutputDirectory = "",
    [string]$ContractsPath = "",
    [switch]$MainToolbox
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if ([String]::IsNullOrWhiteSpace($ContractsPath)) {
    $contractsBuild = Join-Path $projectRoot "work\module-builds\translation-contracts"
    & (Join-Path $PSScriptRoot "build-contracts.ps1") `
        -OutputDirectory $contractsBuild | Out-Null
    $ContractsPath = Join-Path $contractsBuild "StructuralToolbox.Contracts.dll"
}
if (-not (Test-Path -LiteralPath $ContractsPath -PathType Leaf)) {
    throw "Contracts assembly was not found: $ContractsPath"
}
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "work\module-builds\drawing-translation"
}
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputDirectoryFull -Force | Out-Null
foreach ($assembly in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $AutoCADDir $assembly) -PathType Leaf)) {
        throw "AutoCAD managed API was not found: $assembly"
    }
}
$outputPath = Join-Path $outputDirectoryFull "StructuralToolbox.DrawingTranslation.dll"
$arguments = @(
    "/nologo", "/target:library", "/platform:x64", "/optimize+", "/warn:4",
    "/utf8output", "/out:$outputPath",
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    "/reference:System.Net.Http.dll", "/reference:System.Security.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:$ContractsPath"
)
if ($MainToolbox) {
    $arguments += "/define:MAIN_TOOLBOX"
}
$arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Sort-Object FullName | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Drawing translation module build failed with exit code $LASTEXITCODE"
}
Write-Host "Built: $outputPath"
