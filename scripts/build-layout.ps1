param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$OutputDirectory = "",
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Layout"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot "work\module-builds\layout" }
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputDirectoryFull -Force | Out-Null
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw "C# compiler was not found: $compiler" }
foreach ($assembly in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll", "AdWindows.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $AutoCADDir $assembly) -PathType Leaf)) { throw "AutoCAD managed API was not found: $(Join-Path $AutoCADDir $assembly)" }
}
$outputPath = Join-Path $outputDirectoryFull "StructuralToolbox.Layout.dll"
$arguments = @(
    "/nologo", "/target:library", "/platform:x64", "/optimize+", "/warn:4", "/utf8output", "/out:$outputPath",
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')", "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')", "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')", "/reference:$(Join-Path $AutoCADDir 'AdWindows.dll')",
    "/reference:System.dll", "/reference:System.Core.dll", "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll", "/reference:System.Xml.dll"
)
$arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "Layout module build failed with exit code $LASTEXITCODE" }
Write-Host "Built: $outputPath"
if ($PassThru) { [pscustomobject]@{ name = "StructuralToolbox.Layout"; path = $outputPath; sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash } }
