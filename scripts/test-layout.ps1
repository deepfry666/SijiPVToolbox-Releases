param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\layout-tests"
$testOutput = Join-Path $testDirectory "LayoutEngineTests.exe"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

$sources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\Core\LayoutGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\Core\LayoutEngine.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\Core\ObstacleSelectionSummary.cs"),
    (Join-Path $projectRoot "tests\LayoutEngineTests.cs")
)

& $compiler /nologo /target:exe /optimize+ "/out:$testOutput" $sources
if ($LASTEXITCODE -ne 0) {
    throw "Layout engine test build failed with exit code $LASTEXITCODE"
}
& $testOutput
if ($LASTEXITCODE -ne 0) {
    throw "Layout engine tests failed with exit code $LASTEXITCODE"
}
Write-Host "LAYOUT_CORE_TEST=PASS"
