param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\layout-tests"
$testOutput = Join-Path $testDirectory "LayoutDialogTests.exe"
$screenshot = Join-Path $testDirectory "LayoutDialog-200percent.png"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

$sources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\Core\LayoutGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\Core\LayoutEngine.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\Settings\LayoutSettings.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\UI\LayoutDiagram.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Layout\UI\LayoutDialog.cs"),
    (Join-Path $projectRoot "tests\LayoutDialogTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /reference:System.dll `
    /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll "/out:$testOutput" $sources
if ($LASTEXITCODE -ne 0) {
    throw "Layout dialog test build failed with exit code $LASTEXITCODE"
}
& $testOutput $screenshot
if ($LASTEXITCODE -ne 0) {
    throw "Layout dialog tests failed with exit code $LASTEXITCODE"
}
Write-Host "LAYOUT_DIALOG_TEST=PASS"
Write-Host "LAYOUT_DIALOG_SCREENSHOT=$screenshot"
