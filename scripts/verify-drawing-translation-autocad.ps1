param([string]$AutoCADDir = "D:\cad\AutoCAD 2020")

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$templateName = (-join @([char]27169, [char]26495, [char]22270)) + ".dwg"
$template = Join-Path $projectRoot $templateName
foreach ($required in @($compiler, $coreConsole, $template)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required verification file was not found: $required"
    }
}
$templateHash = (Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash
$runName = "drawing-translation-" + [guid]::NewGuid().ToString("N")
$runRoot = Join-Path $projectRoot ("work\" + $runName)
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$fixture = Join-Path $runRoot "fixture.dwg"
$replaceFixture = Join-Path $runRoot "replace-fixture.dwg"
$harness = Join-Path $runRoot "DrawingTranslationCadHarness.dll"
$contractsPath = Join-Path $runRoot "StructuralToolbox.Contracts.dll"
Copy-Item -LiteralPath $template -Destination $fixture
Copy-Item -LiteralPath $template -Destination $replaceFixture

& (Join-Path $PSScriptRoot "build-contracts.ps1") `
    -OutputDirectory $runRoot | Out-Null
if (-not (Test-Path -LiteralPath $contractsPath -PathType Leaf)) {
    throw "Contracts build was not created: $contractsPath"
}

$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation"
$sources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -ne "AssemblyInfo.cs" } |
    Sort-Object FullName | ForEach-Object { $_.FullName })
$sources += Join-Path $projectRoot "tests\DrawingTranslationCadTests.cs"
$references = @(
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    "/reference:System.Net.Http.dll", "/reference:System.Security.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:$contractsPath"
)
& $compiler /nologo /target:library /platform:x64 /optimize+ `
    /utf8output "/out:$harness" $references $sources
if ($LASTEXITCODE -ne 0) { throw "CAD harness build failed." }

$existingQ = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdQ = $false
if ($null -eq $existingQ) {
    & subst.exe Q: $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "Could not map project root to Q:." }
    $createdQ = $true
} elseif (-not (Test-Path -LiteralPath "Q:\scripts\verify-drawing-translation-autocad.ps1")) {
    throw "Q: is already mapped outside this project."
}

function Convert-ToQPath([string]$path) {
    $relative = $path.Substring($projectRoot.Length).TrimStart("\")
    return "Q:/" + $relative.Replace("\", "/")
}

try {
    foreach ($step in @(
        [pscustomobject]@{ Name="replace"; Command="STPVTRANSLATIONREPLACECHECK";
            Token="STPV_TRANSLATION_REPLACE_PASS"; Save=$true; Fixture=$replaceFixture },
        [pscustomobject]@{ Name="replace-reopen"; Command="STPVTRANSLATIONREPLACEREOPEN";
            Token="STPV_TRANSLATION_REPLACE_REOPEN_PASS"; Save=$false; Fixture=$replaceFixture }
    )) {
        $script = Join-Path $runRoot ($step.Name + ".scr")
        $lines = @(
            '(setvar "REPORTERROR" 0)',
            '(setvar "SECURELOAD" 0)',
            '_.NETLOAD',
            (Convert-ToQPath $harness),
            $step.Command
        )
        if ($step.Save) { $lines += '_.QSAVE' }
        $lines += '_.QUIT'
        [IO.File]::WriteAllLines($script, $lines, [Text.Encoding]::ASCII)
        $output = & $coreConsole /i (Convert-ToQPath $step.Fixture) `
            /s (Convert-ToQPath $script) /l chs `
            /isolate ("STPV_TRANSLATION_" + $step.Name.ToUpperInvariant()) `
            (Convert-ToQPath $runRoot) 2>&1
        $exitCode = $LASTEXITCODE
        $log = ($output | Out-String).Replace([string][char]0, "")
        $logPath = Join-Path $runRoot ($step.Name + ".log")
        [IO.File]::WriteAllText($logPath, $log,
            [Text.UTF8Encoding]::new($false))
        if ($exitCode -ne 0 -or -not $log.Contains($step.Token)) {
            throw "CAD $($step.Name) verification failed: $logPath"
        }
        Write-Host $step.Token
    }
} finally {
    if ($createdQ) { & subst.exe Q: /D | Out-Null }
}
if ((Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash -ne
    $templateHash) {
    throw "Original DWG template changed during verification."
}
Write-Host "VERIFICATION_DIR=$runRoot"
