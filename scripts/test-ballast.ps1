param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\ballast-tests"
$layoutTestOutput = Join-Path $testDirectory "BallastLayoutEngineTests.exe"
$quantityTestOutput = Join-Path $testDirectory "QuantityStatisticsTests.exe"
$pierIdentityTestOutput = Join-Path $testDirectory "BallastPierIdentityTests.exe"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

$layoutSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionEngine.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionStatistics.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition\Core\ArrayRecognitionFingerprint.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastLayoutEngine.cs"),
    (Join-Path $projectRoot "tests\BallastLayoutEngineTests.cs")
)

& $compiler /nologo /target:exe /optimize+ "/out:$layoutTestOutput" $layoutSources
if ($LASTEXITCODE -ne 0) {
    throw "Ballast layout test build failed with exit code $LASTEXITCODE"
}

& $layoutTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Ballast layout tests failed with exit code $LASTEXITCODE"
}

$quantitySources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastStatistics.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\RailStatistics.cs"),
    (Join-Path $projectRoot "tests\QuantityStatisticsTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$quantityTestOutput" $quantitySources
if ($LASTEXITCODE -ne 0) {
    throw "Quantity statistics test build failed with exit code $LASTEXITCODE"
}

& $quantityTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Quantity statistics tests failed with exit code $LASTEXITCODE"
}

$pierIdentitySources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastStatistics.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastPierIdentity.cs"),
    (Join-Path $projectRoot "tests\BallastPierIdentityTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$pierIdentityTestOutput" $pierIdentitySources
if ($LASTEXITCODE -ne 0) {
    throw "Ballast pier identity test build failed with exit code $LASTEXITCODE"
}

& $pierIdentityTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Ballast pier identity tests failed with exit code $LASTEXITCODE"
}

Write-Host "BALLAST_CORE_TEST=PASS"
