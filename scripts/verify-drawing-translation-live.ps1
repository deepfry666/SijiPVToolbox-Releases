param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [ValidateSet("replace", "undo", "api", "all")]
    [string]$Mode = "all"
)

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
$runName = "live-translation-" + [guid]::NewGuid().ToString("N")
$runRoot = Join-Path $projectRoot ("work\" + $runName)
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$harness = Join-Path $runRoot "DrawingTranslationLiveHarness.dll"
$contractsPath = Join-Path $runRoot "StructuralToolbox.Contracts.dll"

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
& $compiler /nologo /target:library /platform:x64 /optimize+ /utf8output `
    "/out:$harness" $references $sources
if ($LASTEXITCODE -ne 0) { throw "Live translation harness build failed." }

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
function Invoke-CoreConsole([string]$drawing, [string[]]$lines, [string]$tag) {
    $script = Join-Path $runRoot ($tag + ".scr")
    [IO.File]::WriteAllLines($script, $lines, [Text.Encoding]::ASCII)
    $output = & $coreConsole /i (Convert-ToQPath $drawing) `
        /s (Convert-ToQPath $script) /l chs `
        /isolate ("STPV_LIVE_" + $tag.ToUpperInvariant()) `
        (Convert-ToQPath $runRoot) 2>&1
    $exitCode = $LASTEXITCODE
    $log = ($output | Out-String).Replace([string][char]0, "")
    $logPath = Join-Path $runRoot ($tag + ".log")
    [IO.File]::WriteAllText($logPath, $log, [Text.UTF8Encoding]::new($false))
    Write-Host ("CORE_CONSOLE " + $tag + " exit=" + $exitCode + " log=" + $logPath)
    return $log
}
function Read-Dump([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Text dump was not written: $path"
    }
    $items = New-Object System.Collections.ArrayList
    foreach ($line in [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)) {
        $parts = $line.Split([char]'|', 4)
        if ($parts.Count -lt 4) { continue }
        [void]$items.Add([pscustomobject]@{
            Kind = $parts[0]; Handle = $parts[1]; Y = $parts[2]
            Content = $parts[3] })
    }
    return @($items)
}
function Count-Cjk([string]$value, [string]$pattern) {
    return ([regex]::Matches($value, $pattern)).Count
}
function Invoke-LiveMode([string]$name, [string]$command, [string]$token) {
    $drawing = Join-Path $runRoot ($name + ".dwg")
    Copy-Item -LiteralPath $template -Destination $drawing
    $liveLog = Invoke-CoreConsole $drawing @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        (Convert-ToQPath $harness),
        $command,
        '_.QSAVE',
        '_.QUIT'
    ) ($name + "-live")
    if (-not $liveLog.Contains($token)) {
        $failure = [regex]::Match($liveLog,
            "STPV_TRANSLATION_LIVE_FAIL[^\r\n]*")
        throw ($name + " mode did not translate: " +
            $(if ($failure.Success) { $failure.Value } else { "no result token in log" }))
    }
    Write-Host ("LIVE_RESULT " + $name + " " +
        ([regex]::Match($liveLog, $token + "[^\r\n]*").Value.Trim()))
    $checkLog = Invoke-CoreConsole $drawing @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        (Convert-ToQPath $harness),
        'STPVTRANSLATIONLIVECHECK',
        '_.QUIT'
    ) ($name + "-check")
    $checkToken = [regex]::Match($checkLog,
        "STPV_TRANSLATION_LIVECHECK[^\r\n]*")
    if (-not $checkToken.Success) {
        throw ($name + " reopen check did not report a result.")
    }
    Write-Host ("LIVE_CHECK " + $name + " " + $checkToken.Value.Trim())
    $prefix = [IO.Path]::GetFileNameWithoutExtension($drawing)
    return [pscustomobject]@{
        Name = $name
        Drawing = $drawing
        Before = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-before.txt")))
        After = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-after.txt")))
        Check = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-check.txt")))
    }
}
function Assert-ReplaceResult($result) {
    $before = $result.Before
    $after = $result.After
    $check = $result.Check
    if ($before.Count -eq 0) { throw "No text was dumped before translation." }
    if ($before.Count -ne $after.Count -or $after.Count -ne $check.Count) {
        throw ("Text count changed in replace mode: " + $before.Count +
            " -> " + $after.Count + " -> " + $check.Count)
    }
    $changed = 0
    $cjk = 0
    for ($index = 0; $index -lt $before.Count; $index++) {
        if ($after[$index].Kind -ne $check[$index].Kind -or
            $after[$index].Handle -ne $check[$index].Handle -or
            $after[$index].Y -ne $check[$index].Y -or
            $after[$index].Content -ne $check[$index].Content) {
            throw ("Saved drawing differs from the live result at index " + $index)
        }
        $cjk += Count-Cjk $after[$index].Content "[\u3400-\u9fff]"
        if ($before[$index].Content -ne $after[$index].Content) {
            $changed++
            Write-Host ("PAIR replace " + $before[$index].Kind + " " +
                $before[$index].Handle + ": " + $before[$index].Content +
                " => " + $after[$index].Content)
        }
    }
    Write-Host ("REPLACE_ITEMS=" + $before.Count + " CHANGED=" + $changed +
        " CJK_AFTER=" + $cjk)
    if ($changed -eq 0) { throw "Replace mode did not change any text." }
    if ($cjk -ne 0) { throw "Replace mode left Chinese characters in the drawing." }
}
function Invoke-UndoMode {
    $name = "undo"
    $drawing = Join-Path $runRoot ($name + ".dwg")
    Copy-Item -LiteralPath $template -Destination $drawing
    $liveLog = Invoke-CoreConsole $drawing @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        (Convert-ToQPath $harness),
        'STPVTRANSLATIONLIVE',
        '_.UNDO',
        '1',
        'STPVTRANSLATIONLIVECHECK',
        '_.QSAVE',
        '_.QUIT'
    ) ($name + "-live")
    if (-not $liveLog.Contains("STPV_TRANSLATION_LIVE_PASS")) {
        $failure = [regex]::Match($liveLog,
            "STPV_TRANSLATION_LIVE_FAIL[^\r\n]*")
        throw ("undo mode did not translate: " +
            $(if ($failure.Success) { $failure.Value } else { "no result token in log" }))
    }
    $undoToken = [regex]::Match($liveLog, "STPV_TRANSLATION_LIVECHECK[^\r\n]*")
    if (-not $undoToken.Success) {
        throw "undo mode did not report a text dump after UNDO."
    }
    Write-Host ("LIVE_CHECK undo " + $undoToken.Value.Trim())
    $prefix = [IO.Path]::GetFileNameWithoutExtension($drawing)
    $inSession = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-check.txt")))
    $reopenLog = Invoke-CoreConsole $drawing @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        (Convert-ToQPath $harness),
        'STPVTRANSLATIONLIVECHECK',
        '_.QUIT'
    ) ($name + "-reopen")
    $reopenToken = [regex]::Match($reopenLog,
        "STPV_TRANSLATION_LIVECHECK[^\r\n]*")
    if (-not $reopenToken.Success) {
        throw "undo reopen check did not report a result."
    }
    Write-Host ("LIVE_CHECK undo-reopen " + $reopenToken.Value.Trim())
    return [pscustomobject]@{
        Name = $name
        Drawing = $drawing
        Before = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-before.txt")))
        After = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-after.txt")))
        Undone = $inSession
        Check = @(Read-Dump (Join-Path $runRoot ($prefix + ".translation-check.txt")))
    }
}
function Assert-UndoResult($result) {
    if ($result.Before.Count -eq 0) { throw "No text was dumped before translation." }
    if ($result.After.Count -ne $result.Before.Count) {
        throw ("Text count changed before undo: " + $result.Before.Count +
            " -> " + $result.After.Count)
    }
    $changed = 0
    $cjk = 0
    for ($index = 0; $index -lt $result.Before.Count; $index++) {
        if ($result.After[$index].Content -ne $result.Before[$index].Content) {
            $changed++
        }
        $cjk += Count-Cjk $result.After[$index].Content "[\u3400-\u9fff]"
    }
    if ($changed -eq 0) { throw "Undo mode did not translate any text first." }
    if ($cjk -ne 0) { throw "Translated text still contains Chinese before undo." }
    foreach ($dump in @($result.Undone, $result.Check)) {
        if ($dump.Count -ne $result.Before.Count) {
            throw ("Undo restored " + $dump.Count + " text items instead of " +
                $result.Before.Count)
        }
        for ($index = 0; $index -lt $dump.Count; $index++) {
            if ($dump[$index].Handle -ne $result.Before[$index].Handle -or
                $dump[$index].Y -ne $result.Before[$index].Y -or
                $dump[$index].Content -ne $result.Before[$index].Content) {
                throw ("One UNDO did not restore item " + $index + ": " +
                    $dump[$index].Content + " <> " +
                    $result.Before[$index].Content)
            }
        }
    }
    $restored = 0
    foreach ($item in $result.Check) {
        $restored += Count-Cjk $item.Content "[\u3400-\u9fff]"
    }
    Write-Host ("UNDO_RESTORED=" + $result.Before.Count +
        " CHANGED_BEFORE_UNDO=" + $changed + " CJK_AFTER_UNDO=" + $restored)
}
function Invoke-ApiProbe {
    $drawing = Join-Path $runRoot "api-probe.dwg"
    Copy-Item -LiteralPath $template -Destination $drawing
    $log = Invoke-CoreConsole $drawing @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        (Convert-ToQPath $harness),
        'STPVTRANSLATIONLIVEAPI',
        '_.QUIT'
    ) "api-probe"
    $match = [regex]::Match($log,
        "STPV_TRANSLATION_LIVE_API_(PASS|FAIL)[^\r\n]*")
    if (-not $match.Success) { throw "API probe did not report a result." }
    if ($match.Groups[1].Value -ne "PASS") {
        throw ("API probe failed: " + $match.Value.Trim())
    }
    if ($match.Value -match "sample=\u7ed3\u6784\u5de5\u5177\u7bb1\u8fde\u63a5\u6d4b\u8bd5") {
        throw "API probe returned the untranslated source text."
    }
    Write-Host ("LIVE_API " + $match.Value.Trim())
}
$undoResult = $null
$results = New-Object System.Collections.ArrayList
try {
    if ($Mode -eq "replace" -or $Mode -eq "all") {
        $replace = Invoke-LiveMode "replace" "STPVTRANSLATIONLIVE" `
            "STPV_TRANSLATION_LIVE_PASS"
        Assert-ReplaceResult $replace
        [void]$results.Add($replace)
    }
    if ($Mode -eq "api" -or $Mode -eq "all") {
        Invoke-ApiProbe
    }
    if ($Mode -eq "undo" -or $Mode -eq "all") {
        $undoResult = Invoke-UndoMode
        Assert-UndoResult $undoResult
    }
} finally {
    if ($createdQ) { & subst.exe Q: /D | Out-Null }
}
if ((Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash -ne
    $templateHash) {
    throw "Original DWG template changed during verification."
}
foreach ($result in $results) {
    if ((Get-FileHash -LiteralPath $result.Drawing -Algorithm SHA256).Hash -eq
        $templateHash) {
        throw ("Drawing was not saved for mode " + $result.Name)
    }
    Write-Host ("LIVE_TRANSLATION_DRAWING " + $result.Name + "=" +
        $result.Drawing)
}
Write-Host "LIVE_TRANSLATION_DIR=$runRoot"
Write-Host ("PASS live drawing translation (" + $Mode +
    ") against the configured API")
