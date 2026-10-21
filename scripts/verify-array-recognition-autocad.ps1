param([string]$AutoCADDir = "D:\cad\AutoCAD 2020")

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$template = Join-Path $projectRoot "模板图.dwg"
foreach ($path in @($coreConsole, $template)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "AutoCAD verification dependency was not found: $path"
    }
}

$runRoot = Join-Path $projectRoot `
    ("work\array-recognition-autocad\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$inputDwg = Join-Path $runRoot "issue0017-test.dwg"
Copy-Item -LiteralPath $template -Destination $inputDwg -Force
$contracts = & (Join-Path $PSScriptRoot "build-contracts.ps1") `
    -OutputDirectory (Join-Path $runRoot "build") -PassThru
$ballast = & (Join-Path $PSScriptRoot "build-ballast.ps1") `
    -AutoCADDir $AutoCADDir -OutputDirectory (Join-Path $runRoot "build") `
    -ContractsPath $contracts.path -PassThru
$array = & (Join-Path $PSScriptRoot "build-array-recognition.ps1") `
    -AutoCADDir $AutoCADDir -OutputDirectory (Join-Path $runRoot "build") `
    -PassThru

$qDrive = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdQ = $false
if ($null -eq $qDrive) {
    subst Q: $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "Unable to map project root to Q:." }
    $createdQ = $true
} elseif ([IO.Path]::GetFullPath($qDrive.Root) -ne `
        [IO.Path]::GetFullPath($projectRoot)) {
    throw "Q: is already mapped to another location."
}

$relativeRun = $runRoot.Substring($projectRoot.Length).TrimStart("\").Replace("\", "/")
$firstScript = Join-Path $runRoot "run.scr"
$secondScript = Join-Path $runRoot "reopen.scr"
$harness = "Q:/tests/ArrayRecognitionAutoCadHarness.lsp"
$arrayDll = "Q:/$relativeRun/build/StructuralToolbox.ArrayRecognition.dll"
$contractsDll = "Q:/$relativeRun/build/StructuralToolbox.Contracts.dll"
$ballastDll = "Q:/$relativeRun/build/StructuralToolbox.Ballast.dll"
$firstLines = @(
    '(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)',
    '_.NETLOAD', $arrayDll, '_.NETLOAD', $contractsDll,
    '_.NETLOAD', $ballastDll, "(load `"$harness`")",
    '(issue17-prepare)', '(issue17-run)', '_.QUIT'
)
$secondLines = @(
    '(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)',
    "(load `"$harness`")", '(issue17-reopen)', '_.QUIT'
)
[IO.File]::WriteAllLines($firstScript, $firstLines, [Text.Encoding]::ASCII)
[IO.File]::WriteAllLines($secondScript, $secondLines, [Text.Encoding]::ASCII)

function Invoke-Core([string]$Script, [string]$Log, [string]$Isolate) {
    $output = & $coreConsole /i $inputDwg /s $Script /l chs `
        /isolate $Isolate (Join-Path $runRoot $Isolate) *>&1
    $exit = $LASTEXITCODE
    $text = ($output | Out-String).Replace([string][char]0, "")
    [IO.File]::WriteAllText($Log, $text, [Text.UTF8Encoding]::new($false))
    Write-Host $text
    if ($exit -ne 0) { throw "Core Console failed: $Log (exit $exit)" }
    return $text
}

try {
    $firstLog = Join-Path $runRoot "01-run.log"
    $secondLog = Join-Path $runRoot "02-reopen.log"
    $first = Invoke-Core $firstScript $firstLog "ISSUE17_RUN"
    $second = Invoke-Core $secondScript $secondLog "ISSUE17_REOPEN"
    foreach ($marker in @("ISSUE17_PREPARED=PASS",
        "ISSUE17_WINDOW_OBJECTS=7",
        "ISSUE17_CANCEL_OBJECTS=7,7",
        "ISSUE17_INDEPENDENT_TABLE=0,1,0",
        "ISSUE17_BALLAST_TABLE=1,2,1", "ISSUE17_RUN=PASS")) {
        if (-not $first.Contains($marker)) { throw "Missing marker: $marker" }
    }
    if ($first -notmatch 'ISSUE17_BALLAST_FRAMES=(?<count>[1-9][0-9]*)') {
        throw "No generated ballast frames were recorded."
    }
    foreach ($marker in @("ISSUE17_REOPEN_TABLES=2",
        "ISSUE17_REOPEN=PASS")) {
        if (-not $second.Contains($marker)) { throw "Missing marker: $marker" }
    }
    if ($second -notmatch 'ISSUE17_REOPEN_BALLAST_FRAMES=(?<count>[1-9][0-9]*)') {
        throw "Generated ballast frames were not preserved after reopen."
    }
    $result = [ordered]@{
        status = "PASS"; runRoot = $runRoot; drawing = $inputDwg
        arrayDllSha256 = $array.sha256; ballastDllSha256 = $ballast.sha256
        drawingSha256 = (Get-FileHash $inputDwg -Algorithm SHA256).Hash
        firstLog = $firstLog; reopenLog = $secondLog
    }
    $result | ConvertTo-Json -Depth 3 | Set-Content `
        -LiteralPath (Join-Path $runRoot "result.json") -Encoding UTF8
    Write-Host "ISSUE17_AUTOCAD2020=PASS"
    Write-Host "RESULT=$(Join-Path $runRoot 'result.json')"
} finally {
    if ($createdQ) { subst Q: /D | Out-Null }
}
