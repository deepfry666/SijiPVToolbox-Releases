param()
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\layout-tests"
$testOutput = Join-Path $testDirectory "LayoutDiagramTests.exe"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$sources = @((Join-Path $projectRoot "src\StructuralToolbox.Layout\UI\LayoutDiagram.cs"), (Join-Path $projectRoot "tests\LayoutDiagramTests.cs"))
& $compiler /nologo /target:exe /optimize+ /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$testOutput" $sources
if ($LASTEXITCODE -ne 0) { throw "Layout diagram test build failed with exit code $LASTEXITCODE" }
& $testOutput
if ($LASTEXITCODE -ne 0) { throw "Layout diagram tests failed with exit code $LASTEXITCODE" }
Write-Host "LAYOUT_DIAGRAM_TEST=PASS"
