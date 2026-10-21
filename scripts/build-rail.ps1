param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$OutputDirectory = "",
    [string]$ContractsPath = "",
    [string]$OutputName = "StructuralToolbox.Rail.dll",
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Rail"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfAssemblyRoot = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "work\module-builds\rail"
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

foreach ($assembly in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll", "AdWindows.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $AutoCADDir $assembly) -PathType Leaf)) {
        throw "AutoCAD managed API was not found: $(Join-Path $AutoCADDir $assembly)"
    }
}

$outputPath = Join-Path $outputDirectoryFull $OutputName
$arguments = @(
    "/nologo", "/target:library", "/platform:x64", "/optimize+", "/warn:4",
    "/utf8output", "/out:$outputPath", "/reference:$contractsFullPath",
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'AdWindows.dll')",
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    "/reference:System.Xml.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll",
    "/reference:$(Join-Path $wpfAssemblyRoot 'WindowsBase.dll')",
    "/reference:$(Join-Path $wpfAssemblyRoot 'PresentationCore.dll')",
    "/reference:$(Join-Path $wpfAssemblyRoot 'PresentationFramework.dll')"
)
$arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Sort-Object FullName | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Rail module build failed with exit code $LASTEXITCODE"
}
Write-Host "Built: $outputPath"
if ($PassThru) {
    [pscustomobject]@{
        name = "StructuralToolbox.Rail"
        path = $outputPath
        sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
    }
}
