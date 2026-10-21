param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$OutputDirectory = "",
    [string]$ContractsPath = "",
    [string]$OutputName = "StructuralToolbox.SupportDetail.dll",
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.SupportDetail"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot `
        "work\module-builds\support-detail"
}
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputDirectoryFull -Force | Out-Null
if ([String]::IsNullOrWhiteSpace($ContractsPath)) {
    $contract = & (Join-Path $PSScriptRoot "build-contracts.ps1") `
        -OutputDirectory $outputDirectoryFull -PassThru
    $ContractsPath = $contract.path
}
$contractsFullPath = [IO.Path]::GetFullPath($ContractsPath)
if (-not (Test-Path -LiteralPath $contractsFullPath -PathType Leaf)) {
    throw "Contracts assembly was not found: $contractsFullPath"
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler was not found: $compiler"
}
foreach ($assembly in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")) {
    $path = Join-Path $AutoCADDir $assembly
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "AutoCAD managed API was not found: $path"
    }
}

$outputPath = Join-Path $outputDirectoryFull $OutputName
$librarySource = Join-Path $sourceRoot `
    "Library\SupportDetailFixedDetails.dwg"
if (-not (Test-Path -LiteralPath $librarySource -PathType Leaf)) {
    throw "The fixed detail library was not found: $librarySource"
}
$arguments = @(
    "/nologo", "/target:library", "/platform:x64", "/optimize+", "/warn:4",
    "/utf8output", "/out:$outputPath",
    "/reference:$contractsFullPath",
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    "/reference:System.Xml.dll",
    "/resource:$librarySource,StructuralToolbox.SupportDetail.SupportDetailFixedDetails.dwg"
)
$arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Sort-Object FullName | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Support detail module build failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $outputPath"
if ($PassThru) {
    [pscustomobject]@{
        name = "StructuralToolbox.SupportDetail"
        path = $outputPath
        sha256 = (Get-FileHash -LiteralPath $outputPath `
            -Algorithm SHA256).Hash
        contracts = $contractsFullPath
        librarySource = $librarySource
    }
}
