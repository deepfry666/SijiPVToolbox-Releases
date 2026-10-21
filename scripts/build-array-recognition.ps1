param([string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$OutputDirectory = "", [switch]$PassThru)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "work\module-builds\array-recognition"
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
foreach ($assembly in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $AutoCADDir $assembly))) {
        throw "AutoCAD managed API was not found: $(Join-Path $AutoCADDir $assembly)"
    }
}
$outputPath = Join-Path $outputRoot "StructuralToolbox.ArrayRecognition.dll"
$arguments = @("/nologo", "/target:library", "/platform:x64", "/optimize+",
    "/warn:4", "/utf8output", "/out:$outputPath",
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:System.dll", "/reference:System.Core.dll")
$arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Sort-Object FullName | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "Array recognition build failed: $LASTEXITCODE" }
Write-Host "Built: $outputPath"
if ($PassThru) { [pscustomobject]@{ name="StructuralToolbox.ArrayRecognition";
    path=$outputPath; sha256=(Get-FileHash $outputPath -Algorithm SHA256).Hash } }
