param([string]$AutoCADDir = "D:\cad\AutoCAD 2020")

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$templateName = ([string][char]0x6A21) + ([char]0x677F) +
    ([char]0x56FE) + ".dwg"
$template = Join-Path $projectRoot $templateName
foreach ($path in @($coreConsole, $template)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "AutoCAD verification dependency was not found: $path"
    }
}

$runRoot = Join-Path $projectRoot `
    ("work\ballast-pier-autocad\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$inputDwg = Join-Path $runRoot "issue0030-test.dwg"
Copy-Item -LiteralPath $template -Destination $inputDwg -Force
$contracts = & (Join-Path $PSScriptRoot "build-contracts.ps1") `
    -OutputDirectory (Join-Path $runRoot "build") -PassThru
$ballast = & (Join-Path $PSScriptRoot "build-ballast.ps1") `
    -AutoCADDir $AutoCADDir -OutputDirectory (Join-Path $runRoot "build") `
    -ContractsPath $contracts.path -PassThru

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
$setupHarness = "Q:/tests/ArrayRecognitionAutoCadHarness.lsp"
$pierHarness = "Q:/tests/BallastPierRecognitionAutoCadHarness.lsp"
$contractsDll = "Q:/$relativeRun/build/StructuralToolbox.Contracts.dll"
$ballastDll = "Q:/$relativeRun/build/StructuralToolbox.Ballast.dll"
$firstLines = @(
    '(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)',
    '_.NETLOAD', $contractsDll, '_.NETLOAD', $ballastDll,
    "(load `"$setupHarness`")", "(load `"$pierHarness`")",
    '(issue30-run)', '_.QUIT'
)
$secondLines = @(
    '(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)',
    "(load `"$pierHarness`")", '(issue30-reopen)', '_.QUIT'
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
    $first = Invoke-Core $firstScript $firstLog "ISSUE30_RUN"
    $second = Invoke-Core $secondScript $secondLog "ISSUE30_REOPEN"
    foreach ($marker in @("ISSUE30_MANUAL_MARKER=1",
        "ISSUE30_STANDALONE_CREATED=1",
        "ISSUE30_RUN=PASS")) {
        if (-not $first.Contains($marker)) { throw "Missing marker: $marker" }
    }
    $nestedMatch = [regex]::Match($first,
        'ISSUE30_FRAME_PIERS=(?<value>[1-9][0-9]*)')
    $expectedMatch = [regex]::Match($first,
        'ISSUE30_EXPECTED_TOTAL=(?<value>[1-9][0-9]*)')
    $actualMatch = [regex]::Match($first,
        'ISSUE30_ACTUAL_TOTAL=(?<value>[1-9][0-9]*)')
    if (-not $nestedMatch.Success -or -not $expectedMatch.Success -or
        -not $actualMatch.Success -or
        [int]$expectedMatch.Groups['value'].Value -ne
            [int]$actualMatch.Groups['value'].Value -or
        [int]$actualMatch.Groups['value'].Value -lt 3) {
        throw "Single-pier totals were not recorded."
    }
    foreach ($marker in @("ISSUE30_REOPEN_TABLES=1",
        "ISSUE30_REOPEN=PASS")) {
        if (-not $second.Contains($marker)) { throw "Missing marker: $marker" }
    }
    $result = [ordered]@{
        status = "PASS"; runRoot = $runRoot; drawing = $inputDwg
        ballastDllSha256 = $ballast.sha256
        drawingSha256 = (Get-FileHash $inputDwg -Algorithm SHA256).Hash
        firstLog = $firstLog; reopenLog = $secondLog
    }
    $result | ConvertTo-Json -Depth 3 | Set-Content `
        -LiteralPath (Join-Path $runRoot "result.json") -Encoding UTF8
    Write-Host "ISSUE30_AUTOCAD2020=PASS"
    Write-Host "RESULT=$(Join-Path $runRoot 'result.json')"
} finally {
    if ($createdQ) { subst Q: /D | Out-Null }
}
