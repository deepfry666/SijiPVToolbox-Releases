param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$passed = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:passed++
}

function Get-Code([string]$RelativeRoot) {
    $root = Join-Path $projectRoot $RelativeRoot
    return ((Get-ChildItem -LiteralPath $root -Filter "*.cs" -Recurse |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" -and
            $_.Name -ne "VerificationCommands.cs" } |
        Sort-Object FullName | ForEach-Object {
            Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
        }) -join "`n")
}

$shell = Get-Code "src\StructuralToolbox"
$rail = Get-Code "src\StructuralToolbox.Rail"
$ballast = Get-Code "src\StructuralToolbox.Ballast"
$arrayRecognition = Get-Code "src\StructuralToolbox.ArrayRecognition"
$supportDetail = Get-Code "src\StructuralToolbox.SupportDetail"
$update = Get-Code "src\StructuralToolbox.Update"
$translation = Get-Code "src\StructuralToolbox.DrawingTranslation"
$contracts = Get-Code "src\StructuralToolbox.Contracts"
$internalVerifier = Get-Content -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.Rail\Commands\VerificationCommands.cs") `
    -Raw -Encoding UTF8

Assert-Condition (-not $shell.Contains("StructuralToolbox.Ballast") -and
    -not $shell.Contains("RailToolboxModule") -and
    -not $shell.Contains("BallastToolboxModule") -and
    -not $shell.Contains("new ToolboxControl") -and
    -not $shell.Contains("new BallastToolboxControl")) `
    "shell has no concrete feature-module dependency"
Assert-Condition (-not $rail.Contains("StructuralToolbox.Ballast")) `
    "rail module has no ballast dependency"
Assert-Condition (-not $arrayRecognition.Contains("StructuralToolbox.Ballast") -and
    -not $arrayRecognition.Contains("StructuralToolbox.Rail") -and
    -not $arrayRecognition.Contains("StructuralToolbox.Contracts")) `
    "array recognition remains independently loadable"
Assert-Condition (-not $ballast.Contains("StructuralToolbox.Rail") -and
    -not $ballast.Contains("StructuralToolbox.UI.ToolboxShellControl")) `
    "ballast module has no rail or shell dependency"
Assert-Condition (-not $supportDetail.Contains("StructuralToolbox.Rail") -and
    -not $supportDetail.Contains("StructuralToolbox.Ballast") -and
    -not $supportDetail.Contains("StructuralToolbox.UI.ToolboxShellControl")) `
    "support detail module has no rail, ballast, or shell dependency"
Assert-Condition (-not $update.Contains("Autodesk.AutoCAD") -and
    -not $update.Contains("StructuralToolbox.Rail") -and
    -not $update.Contains("StructuralToolbox.Ballast")) `
    "update module has no AutoCAD, rail, or ballast dependency"
Assert-Condition (-not $translation.Contains("StructuralToolbox.Rail") -and
    -not $translation.Contains("StructuralToolbox.Ballast") -and
    -not $translation.Contains("StructuralToolbox.UI.ToolboxShellControl")) `
    "drawing translation module has no rail, ballast, or shell dependency"
Assert-Condition ($translation.Contains('[CommandMethod("STPVTRANSLATE",') -and
    $translation.Contains('[CommandMethod("STPVTRANSLATECONFIRM",') -and
    -not $translation.Contains('STPVTRANSLATEINSERT') -and
    $translation.Contains('"RunDrawingTranslation"') -and
    $translation.Contains('"RunDrawingTranslationConfirm"')) `
    "drawing translation exposes only direct and confirmed replacement modes"
Assert-Condition (-not $contracts.Contains("Autodesk.AutoCAD")) `
    "module contracts remain independent of AutoCAD runtime APIs"
Assert-Condition ($internalVerifier.TrimStart().StartsWith("#if INTERNAL_VERIFY") -and
    $internalVerifier.TrimEnd().EndsWith("#endif")) `
    "cross-module runtime attestation remains internal-verification-only"

$shellProject = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox\StructuralToolbox.csproj") -Raw
$railProject = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox.Rail\StructuralToolbox.Rail.csproj") -Raw
$ballastProject = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox.Ballast\StructuralToolbox.Ballast.csproj") -Raw
$supportDetailProject = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox.SupportDetail\StructuralToolbox.SupportDetail.csproj") -Raw
$updateProject = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox.Update\StructuralToolbox.Update.csproj") -Raw
$translationProject = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox.DrawingTranslation\StructuralToolbox.DrawingTranslation.csproj") -Raw
foreach ($project in @($shellProject, $railProject, $ballastProject,
        $supportDetailProject, $updateProject, $translationProject)) {
    Assert-Condition ($project.Contains("StructuralToolbox.Contracts.csproj")) `
        "each runtime project references the stable contracts project"
}
Assert-Condition (-not $railProject.Contains("StructuralToolbox.Ballast.csproj") -and
    -not $ballastProject.Contains("StructuralToolbox.Rail.csproj") -and
    -not $supportDetailProject.Contains("StructuralToolbox.Rail.csproj") -and
    -not $supportDetailProject.Contains("StructuralToolbox.Ballast.csproj") -and
    -not $shellProject.Contains("StructuralToolbox.Rail.csproj") -and
    -not $shellProject.Contains("StructuralToolbox.Ballast.csproj") -and
    -not $shellProject.Contains("StructuralToolbox.Update.csproj")) `
    "runtime projects do not reference feature implementations"

$buildScript = Get-Content (Join-Path $projectRoot "scripts\build.ps1") -Raw
Assert-Condition ($buildScript.Contains("build-rail.ps1") -and
    $buildScript.Contains("build-ballast.ps1") -and
    $buildScript.Contains("build-update.ps1") -and
    $buildScript.Contains("build-drawing-translation.ps1") -and
    $buildScript.Contains("build-contracts.ps1") -and
    -not $buildScript.Contains('"build-support-detail.ps1"')) `
    "release build composes only the approved independent modules"

Write-Host "PASS $passed module isolation tests"
