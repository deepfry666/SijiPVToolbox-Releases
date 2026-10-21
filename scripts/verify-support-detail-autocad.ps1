param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$InputDwg = "",
    [string]$ModuleDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$verificationRoot = Join-Path $projectRoot "work\support-detail-verification"
$buildRoot = Join-Path $projectRoot "work\module-builds\support-detail"
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$inputCopy = Join-Path $verificationRoot "support-detail-input.dwg"
$firstScript = Join-Path $verificationRoot "verify.scr"
$reopenScript = Join-Path $verificationRoot "reopen.scr"
$firstLog = Join-Path $verificationRoot "generate-update.log"
$reopenLog = Join-Path $verificationRoot "reopen.log"
$render = Join-Path $verificationRoot "support-detail-check.png"
$librarySource = Join-Path $projectRoot `
    "src\StructuralToolbox.SupportDetail\Library\SupportDetailFixedDetails.dwg"

if ([String]::IsNullOrWhiteSpace($InputDwg)) {
    $templateName = (-join @([char]27169, [char]26495, [char]22270)) + ".dwg"
    $InputDwg = Join-Path $projectRoot $templateName
}
$InputDwg = [IO.Path]::GetFullPath($InputDwg)
if (-not (Test-Path -LiteralPath $InputDwg -PathType Leaf)) {
    throw "Verification input DWG was not found: $InputDwg"
}
if (-not (Test-Path -LiteralPath $coreConsole -PathType Leaf)) {
    throw "AutoCAD 2020 Core Console was not found: $coreConsole"
}
$sourceHashBefore = (Get-FileHash -LiteralPath $InputDwg -Algorithm SHA256).Hash
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
Copy-Item -LiteralPath $InputDwg -Destination $inputCopy -Force

if ([String]::IsNullOrWhiteSpace($ModuleDirectory)) {
    $build = & (Join-Path $PSScriptRoot "build-support-detail.ps1") `
        -AutoCADDir $AutoCADDir -OutputDirectory $buildRoot -PassThru
} else {
    $moduleRoot = [IO.Path]::GetFullPath($ModuleDirectory)
    $modulePath = Join-Path $moduleRoot "StructuralToolbox.SupportDetail.dll"
    $build = [pscustomobject]@{
        path = $modulePath
        librarySource = $librarySource
        sha256 = if (Test-Path -LiteralPath $modulePath -PathType Leaf) {
            (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
        } else {
            $null
        }
    }
}
if (-not (Test-Path -LiteralPath $build.path -PathType Leaf) -or
    [String]::IsNullOrWhiteSpace([string]$build.librarySource) -or
    -not (Test-Path -LiteralPath $build.librarySource -PathType Leaf)) {
    throw "Support detail build did not produce the DLL or fixed library source."
}
$resourceName =
    "StructuralToolbox.SupportDetail.SupportDetailFixedDetails.dwg"
$resourceAssembly = [Reflection.Assembly]::LoadFrom($build.path)
if ($resourceAssembly.GetManifestResourceNames() -notcontains $resourceName) {
    throw "Support detail DLL does not contain the fixed detail library resource."
}
$libraryHashBefore = (Get-FileHash -LiteralPath $build.librarySource `
    -Algorithm SHA256).Hash
$moduleRelative = $build.path.Substring($projectRoot.Length)
$moduleRelative = $moduleRelative.TrimStart("\").Replace("\", "/")

$firstLines = @(
    '(setvar "REPORTERROR" 0)',
    '(setvar "SECURELOAD" 0)',
    '_.NETLOAD',
    ("Q:/" + $moduleRelative),
    'STPVSUPPORTDETAILCHECK',
    '_.ZOOM',
    '_W',
    '-1500,-5000',
    '28000,12000',
    '_.PNGOUT',
    'Q:/work/support-detail-verification/support-detail-check.png',
    '_ALL',
    '',
    '_.QSAVE',
    '_.QUIT'
)
$reopenLines = @(
    '(setvar "REPORTERROR" 0)',
    '(setvar "SECURELOAD" 0)',
    '_.NETLOAD',
    ("Q:/" + $moduleRelative),
    'STPVSUPPORTDETAILREOPENCHECK',
    '_.QUIT'
)
[IO.File]::WriteAllLines($firstScript, $firstLines, [Text.Encoding]::ASCII)
[IO.File]::WriteAllLines($reopenScript, $reopenLines, [Text.Encoding]::ASCII)

function Invoke-CoreConsole {
    param(
        [string]$ScriptPath,
        [string]$LogPath,
        [string]$IsolationName,
        [string]$ExpectedToken
    )

    $isolate = Join-Path $verificationRoot ("isolate-" + $IsolationName)
    New-Item -ItemType Directory -Path $isolate -Force | Out-Null
    $scriptRelative = $ScriptPath.Substring($projectRoot.Length)
    $scriptRelative = $scriptRelative.TrimStart("\").Replace("\", "/")
    $isolateRelative = $isolate.Substring($projectRoot.Length)
    $isolateRelative = $isolateRelative.TrimStart("\").Replace("\", "/")
    $output = & $coreConsole /i `
        Q:/work/support-detail-verification/support-detail-input.dwg `
        /s ("Q:/" + $scriptRelative) /l chs `
        /isolate ("STPV_SUPPORT_" + $IsolationName.ToUpperInvariant()) `
        ("Q:/" + $isolateRelative) 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).Replace([string][char]0, "")
    [IO.File]::WriteAllText($LogPath, $text,
        [Text.UTF8Encoding]::new($false))
    if ($exitCode -ne 0 -or -not $text.Contains($ExpectedToken)) {
        throw "AutoCAD verification failed; expected $ExpectedToken. Log: $LogPath"
    }
    Write-Host $ExpectedToken
}

$qDrive = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdMapping = $false
if ($null -eq $qDrive) {
    & subst.exe Q: $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to map project root to Q:."
    }
    $createdMapping = $true
} else {
    $marker = "Q:\scripts\verify-support-detail-autocad.ps1"
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "Drive Q: is already in use by another location."
    }
}

try {
    Invoke-CoreConsole -ScriptPath $firstScript -LogPath $firstLog `
        -IsolationName "DETAIL_FIRST" `
        -ExpectedToken "SUPPORT_DETAIL_CHECK=PASS"
    Invoke-CoreConsole -ScriptPath $reopenScript -LogPath $reopenLog `
        -IsolationName "DETAIL_REOPEN" `
        -ExpectedToken "SUPPORT_DETAIL_REOPEN=PASS"
} finally {
    if ($createdMapping) {
        & subst.exe Q: /D | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $render -PathType Leaf)) {
    throw "Verification render was not created: $render"
}
$sourceHashAfter = (Get-FileHash -LiteralPath $InputDwg -Algorithm SHA256).Hash
if ($sourceHashAfter -ne $sourceHashBefore) {
    throw "Verification changed the original input DWG."
}
$libraryHashAfter = (Get-FileHash -LiteralPath $build.librarySource `
    -Algorithm SHA256).Hash
if ($libraryHashAfter -ne $libraryHashBefore) {
    throw "Verification changed the fixed detail library."
}

Write-Host "SUPPORT_DETAIL_AUTOCAD2020=PASS"
Write-Host "ORIGINAL_SHA256=$sourceHashAfter"
Write-Host "OUTPUT_DWG=$inputCopy"
Write-Host "OUTPUT_PNG=$render"
Write-Host "MODULE_SHA256=$($build.sha256)"
Write-Host "FIXED_LIBRARY_EMBEDDED=PASS"
Write-Host "FIXED_LIBRARY_SOURCE_SHA256=$libraryHashAfter"
