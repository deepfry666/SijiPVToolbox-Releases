param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$AssemblyPath = "",
    [string]$InputDwg = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $projectRoot `
        "outputs\StructuralToolbox.Layout_v0.1.2_AutoCAD2020\StructuralToolbox.Layout.dll"
}
if ([String]::IsNullOrWhiteSpace($InputDwg)) {
    $templateName = -join @([char]27169, [char]26495, [char]22270) + ".dwg"
    $InputDwg = Join-Path $projectRoot $templateName
}
$AssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
$InputDwg = [IO.Path]::GetFullPath($InputDwg)
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
foreach ($required in @($AssemblyPath, $InputDwg, $coreConsole)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required layout verification file was not found: $required"
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
$runRoot = Join-Path $projectRoot "work\layout-autocad-smoke\$timestamp"
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$inputCopy = Join-Path $runRoot "layout-input.dwg"
Copy-Item -LiteralPath $InputDwg -Destination $inputCopy -Force

$existingQDrive = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdQMapping = $false
if ($null -ne $existingQDrive) {
    $qTestScript = "Q:\scripts\test-layout-autocad.ps1"
    if (-not (Test-Path -LiteralPath $qTestScript -PathType Leaf) -or
        (Get-FileHash -LiteralPath $qTestScript -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash) {
        throw "Drive Q: is already mapped to another location."
    }
} else {
    subst.exe Q: $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Could not map Q: to the project root."
    }
    $createdQMapping = $true
}

try {
    $relativeRun = $runRoot.Substring($projectRoot.Length).TrimStart("\")
    $qRunRoot = Join-Path "Q:\" $relativeRun
    $qInput = Join-Path $qRunRoot "layout-input.dwg"
    $qScript = Join-Path $qRunRoot "coreconsole.scr"
    $qIsolate = Join-Path $qRunRoot "isolate"
    $qTemp = Join-Path $qRunRoot "temp"
    New-Item -ItemType Directory -Path $qIsolate,$qTemp -Force | Out-Null
    $relativeAssembly = $AssemblyPath.Substring($projectRoot.Length).TrimStart("\")
    $qAssembly = (Join-Path "Q:\" $relativeAssembly).Replace("\", "/")
    $scriptLines = @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        $qAssembly,
        'STPVLAYOUTCHECK',
        '_.QSAVE',
        '_.QUIT'
    )
    [IO.File]::WriteAllLines((Join-Path $runRoot "coreconsole.scr"),
        $scriptLines, [Text.Encoding]::ASCII)

    $oldTemp = $env:TEMP
    $oldTmp = $env:TMP
    $env:TEMP = $qTemp
    $env:TMP = $qTemp
    try {
        $output = & $coreConsole /i $qInput /s $qScript /l chs `
            /isolate "STPVLAYOUT_$($timestamp.Replace('-', ''))" $qIsolate *>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $env:TEMP = $oldTemp
        $env:TMP = $oldTmp
    }
    $logText = ($output | Out-String).Replace([string][char]0, "")
    $logPath = Join-Path $runRoot "coreconsole.log"
    [IO.File]::WriteAllText($logPath, $logText,
        [Text.UTF8Encoding]::new($false))
    $markers = [regex]::Matches($logText,
        'STPVLAYOUT-CHECK VERSION=0\.1\.2\.0 CORE=PASS COMPONENTS=\d+ LINES=PASS SUMMARY=PASS BOUNDARY=PASS ROWS=PASS')
    if ($exitCode -ne 0 -or $markers.Count -ne 1 -or
        $logText -match '(?i)unknown command|cannot load|unable to load') {
        throw "AutoCAD layout smoke test failed. Exit=$exitCode MarkerCount=$($markers.Count) Log=$logPath"
    }
    $result = [ordered]@{
        status = "PASS"
        assemblyPath = $AssemblyPath
        assemblySha256 = (Get-FileHash -LiteralPath $AssemblyPath `
            -Algorithm SHA256).Hash
        assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName(
            $AssemblyPath).Version.ToString()
        coreConsolePath = [IO.Path]::GetFullPath($coreConsole)
        coreConsoleExitCode = $exitCode
        marker = $markers[0].Value
        inputCopy = $inputCopy
        logPath = $logPath
    }
    $resultPath = Join-Path $runRoot "result.json"
    [IO.File]::WriteAllText($resultPath,
        ($result | ConvertTo-Json -Depth 4),
        [Text.UTF8Encoding]::new($false))
    Write-Host $markers[0].Value
    Write-Host "LAYOUT_AUTOCAD_SMOKE=PASS"
    Write-Host "LAYOUT_AUTOCAD_RESULT=$resultPath"
} finally {
    if ($createdQMapping) {
        subst.exe Q: /d | Out-Null
    }
}
