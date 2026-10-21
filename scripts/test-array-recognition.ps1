param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\array-recognition-tests"
$testOutput = Join-Path $testDirectory "ArrayRecognitionEngineTests.exe"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

$sources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionEngine.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionStatistics.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionFingerprint.cs"),
    (Join-Path $projectRoot "tests\ArrayRecognitionEngineTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /reference:System.Core.dll `
    "/out:$testOutput" $sources
if ($LASTEXITCODE -ne 0) {
    throw "Array recognition core test build failed with exit code $LASTEXITCODE"
}

& $testOutput
if ($LASTEXITCODE -ne 0) {
    throw "Array recognition core tests failed with exit code $LASTEXITCODE"
}
