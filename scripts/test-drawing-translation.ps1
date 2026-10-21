param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\tests"
$output = Join-Path $testDirectory "DrawingTranslationCoreTests.exe"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$sources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation\Core\DrawingTextModels.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation\Core\TextProtection.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation\Core\TranslationBatch.cs"),
    (Join-Path $projectRoot "tests\DrawingTranslationCoreTests.cs")
)
& $compiler /nologo /target:exe /optimize+ /utf8output `
    "/out:$output" /reference:System.dll /reference:System.Core.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Drawing translation core test build failed." }
& $output
if ($LASTEXITCODE -ne 0) { throw "Drawing translation core tests failed." }
$diagnosticsOutput = Join-Path $testDirectory "DrawingTranslationDiagnosticsTests.exe"
$diagnosticsSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation\Diagnostics\TranslationDiagnostics.cs"),
    (Join-Path $projectRoot "tests\DrawingTranslationDiagnosticsTests.cs")
)
& $compiler /nologo /target:exe /optimize+ /utf8output `
    "/out:$diagnosticsOutput" /reference:System.dll /reference:System.Core.dll `
    $diagnosticsSources
if ($LASTEXITCODE -ne 0) { throw "Drawing translation diagnostics test build failed." }
& $diagnosticsOutput
if ($LASTEXITCODE -ne 0) { throw "Drawing translation diagnostics tests failed." }
$apiOutput = Join-Path $testDirectory "DrawingTranslationApiTests.exe"
$apiSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation\Api\TranslationApi.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation\Api\TranslationCache.cs"),
    (Join-Path $projectRoot "tests\DrawingTranslationApiTests.cs")
)
& $compiler /nologo /target:exe /optimize+ /utf8output `
    "/out:$apiOutput" /reference:System.dll /reference:System.Core.dll `
    /reference:System.Net.Http.dll /reference:System.Security.dll `
    /reference:System.Web.Extensions.dll $apiSources
if ($LASTEXITCODE -ne 0) { throw "Drawing translation API test build failed." }
& $apiOutput
if ($LASTEXITCODE -ne 0) { throw "Drawing translation API tests failed." }
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
$OutputDirectory = Join-Path $projectRoot `
        "work\module-builds\drawing-translation-api"
}
$contractsBuild = Join-Path $projectRoot "work\module-builds\translation-contracts"
& (Join-Path $PSScriptRoot "build-contracts.ps1") `
    -OutputDirectory $contractsBuild | Out-Null
$contractsPath = Join-Path $contractsBuild "StructuralToolbox.Contracts.dll"
& (Join-Path $PSScriptRoot "build-drawing-translation.ps1") `
    -OutputDirectory $OutputDirectory -ContractsPath $contractsPath
if ($LASTEXITCODE -ne 0) { throw "Drawing translation module build failed." }
$uiOutput = Join-Path $testDirectory "DrawingTranslationUiLayoutTests.exe"
Copy-Item -LiteralPath $contractsPath -Destination $testDirectory -Force
$cadDirectory = "D:\cad\AutoCAD 2020"
$uiSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.DrawingTranslation") -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -ne "AssemblyInfo.cs" } |
    Sort-Object FullName | ForEach-Object { $_.FullName })
$uiSources += Join-Path $projectRoot "tests\DrawingTranslationUiLayoutTests.cs"
$references = @(
    "/reference:$(Join-Path $cadDirectory 'acmgd.dll')",
    "/reference:$(Join-Path $cadDirectory 'acdbmgd.dll')",
    "/reference:$(Join-Path $cadDirectory 'accoremgd.dll')",
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    "/reference:System.Net.Http.dll", "/reference:System.Security.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:$contractsPath"
)
& $compiler /nologo /target:exe /platform:x64 /optimize+ /utf8output `
    "/out:$uiOutput" $references $uiSources
if ($LASTEXITCODE -ne 0) { throw "Drawing translation UI test build failed." }
$previousPath = $env:PATH
try {
    $env:PATH = "$cadDirectory;$previousPath"
    & $uiOutput
    if ($LASTEXITCODE -ne 0) { throw "Drawing translation UI tests failed." }
} finally {
    $env:PATH = $previousPath
}
$interactionOutput = Join-Path $testDirectory "DrawingTranslationUiInteractionTests.exe"
$interactionSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.DrawingTranslation") -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -ne "AssemblyInfo.cs" } |
    Sort-Object FullName | ForEach-Object { $_.FullName })
$interactionSources += Join-Path $projectRoot "tests\DrawingTranslationUiInteractionTests.cs"
& $compiler /nologo /target:exe /platform:x64 /optimize+ /utf8output `
    "/out:$interactionOutput" $references $interactionSources
if ($LASTEXITCODE -ne 0) { throw "Drawing translation UI interaction test build failed." }
try {
    $env:PATH = "$cadDirectory;$previousPath"
    & $interactionOutput
    if ($LASTEXITCODE -ne 0) { throw "Drawing translation UI interaction tests failed." }
} finally {
    $env:PATH = $previousPath
}
