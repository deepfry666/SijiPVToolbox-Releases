param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$InputDwg = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$workRoot = Join-Path $projectRoot "work\support-detail-library"
$sourceCopy = Join-Path $workRoot "support-detail-source-copy.dwg"
$extractor = Join-Path $workRoot "SupportDetailLibraryExtractor.dll"
$commandScript = Join-Path $workRoot "extract.scr"
$logPath = Join-Path $workRoot "extract.log"
$output = Join-Path $projectRoot `
    "src\StructuralToolbox.SupportDetail\Library\SupportDetailFixedDetails.dwg"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"

if ([String]::IsNullOrWhiteSpace($InputDwg)) {
    $inspectionCopy = Join-Path $projectRoot `
        "work\support-detail-inspection\support-detail-source-copy.dwg"
    if (Test-Path -LiteralPath $inspectionCopy -PathType Leaf) {
        $InputDwg = $inspectionCopy
    } else {
        $sourceName = (-join @(
            [char]25903, [char]26550, [char]22823, [char]26679,
            [char]31034, [char]24847, [char]22270)) + ".dwg"
        $InputDwg = Join-Path $projectRoot $sourceName
    }
}
if ([String]::IsNullOrWhiteSpace($InputDwg) -or
    -not (Test-Path -LiteralPath $InputDwg -PathType Leaf)) {
    throw "Support detail source DWG was not found."
}
if (-not (Test-Path -LiteralPath $coreConsole -PathType Leaf)) {
    throw "AutoCAD 2020 Core Console was not found: $coreConsole"
}

New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
Copy-Item -LiteralPath $InputDwg -Destination $sourceCopy -Force

$source = Join-Path $projectRoot `
    "scripts\support-detail\ExtractSupportDetailLibrary.cs"
& $compiler /nologo /target:library /platform:x64 /optimize+ /warn:4 `
    "/out:$extractor" `
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')" `
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')" `
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')" `
    /reference:System.dll $source
if ($LASTEXITCODE -ne 0) {
    throw "Support detail library extractor build failed with exit code $LASTEXITCODE"
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
    $resolvedQ = [IO.Path]::GetFullPath("Q:\")
    $marker = Join-Path $resolvedQ "scripts\new-support-detail-library.ps1"
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "Drive Q: is already in use by another location."
    }
}

try {
    $scriptLines = @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        'Q:/work/support-detail-library/SupportDetailLibraryExtractor.dll',
        'SD_EXTRACT_FIXED_DETAILS',
        '_.QUIT',
        '_N'
    )
    [IO.File]::WriteAllLines($commandScript, $scriptLines,
        [Text.Encoding]::ASCII)
    $isolate = Join-Path $workRoot "isolate"
    New-Item -ItemType Directory -Path $isolate -Force | Out-Null
    $consoleOutput = & $coreConsole `
        /i "Q:/work/support-detail-library/support-detail-source-copy.dwg" `
        /s "Q:/work/support-detail-library/extract.scr" /l chs `
        /isolate "STPV_SUPPORT_LIBRARY" `
        "Q:/work/support-detail-library/isolate" 2>&1
    $exitCode = $LASTEXITCODE
    $logText = ($consoleOutput | Out-String).Replace([string][char]0, "")
    [IO.File]::WriteAllText($logPath, $logText,
        [Text.UTF8Encoding]::new($false))
    Write-Host $logText
    if ($exitCode -ne 0 -or
        -not $logText.Contains("SUPPORT_DETAIL_LIBRARY=PASS")) {
        throw "Fixed detail library extraction failed. Log: $logPath"
    }
    if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
        throw "Fixed detail library was not created: $output"
    }
    Write-Host "Created: $output"
    Write-Host "SHA256=$((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash)"
} finally {
    if ($createdMapping) {
        & subst.exe Q: /D | Out-Null
    }
}
