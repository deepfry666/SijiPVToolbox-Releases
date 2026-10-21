$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path -Parent $PSScriptRoot
$testDirectory = Join-Path $projectRoot "work\tests"
$testOutput = Join-Path $testDirectory "BallastPlacementDiagramTests.exe"
$compiler = Join-Path $env:WINDIR `
    "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler was not found: $compiler"
}
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

$sources = @(
    (Join-Path $projectRoot `
        "src\StructuralToolbox.Ballast\UI\BallastPlacementDiagram.cs"),
    (Join-Path $projectRoot "tests\BallastPlacementDiagramTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /reference:System.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/out:$testOutput" $sources
if ($LASTEXITCODE -ne 0) {
    throw "Ballast placement diagram test build failed with exit code $LASTEXITCODE"
}

& $testOutput
if ($LASTEXITCODE -ne 0) {
    throw "Ballast placement diagram tests failed with exit code $LASTEXITCODE"
}

Write-Host "BALLAST_PLACEMENT_DIAGRAM_TEST=PASS"
