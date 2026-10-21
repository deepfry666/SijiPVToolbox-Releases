$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $projectRoot `
    "src\StructuralToolbox.Update\UI\UpdateCheckControl.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$passes = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:passes++
}

$clickStart = $source.IndexOf("private void OnCheckClick",
    [StringComparison]::Ordinal)
$runStart = $source.IndexOf("private void RunCheck",
    [StringComparison]::Ordinal)
Assert-Condition ($clickStart -ge 0 -and $runStart -gt $clickStart) `
    "update click and background check methods are present"

$clickBody = $source.Substring($clickStart, $runStart - $clickStart)
Assert-Condition ($clickBody.Contains("new Thread(new ThreadStart(delegate") -and
    $clickBody.Contains("checkThread.IsBackground = true") -and
    $clickBody.Contains("RunCheck(sourceUris)")) `
    "update checks start on a background thread"
Assert-Condition (-not $clickBody.Contains("FetchManifest(")) `
    "the UI click handler does not perform the network request"

$resultStart = $source.IndexOf("private void ShowCheckResult",
    [StringComparison]::Ordinal)
Assert-Condition ($resultStart -gt $runStart) `
    "update result handler is present"
$runBody = $source.Substring($runStart, $resultStart - $runStart)
Assert-Condition ($runBody.Contains("FetchManifest(sourceUris,") -and
    $runBody.Contains("_cancellation.Token") -and
    $runBody.Contains("RunOnUiThread(delegate")) `
    "background results are marshalled back to the UI thread"
Assert-Condition (-not $source.Contains("UpdateSourceUrl") -and
    -not $source.Contains("UpdateSourceRow") -and
    -not $source.Contains("UpdateSourceHint") -and
    -not $source.Contains('Text = "更新源"') -and
    -not $source.Contains("更新地址已由正式安装包锁定")) `
    "the production update source is not rendered in the update page"
Assert-Condition ($source.Contains("private readonly IList<string> _sourceUrls") -and
    $source.Contains("foreach (string source in _sourceUrls)") -and
    -not $source.Contains('无效的绝对网址：" + sourceText')) `
    "the hidden source remains internal and is not echoed by validation errors"

$dialogPath = Join-Path $projectRoot `
    "src\StructuralToolbox.Update\UI\UpdateResultDialog.cs"
$dialogSource = Get-Content -LiteralPath $dialogPath -Raw -Encoding UTF8
Assert-Condition (-not $dialogSource.Contains("sourceDescription") -and
    $dialogSource.Contains('"发布日期：" + manifest.ReleaseDate') -and
    $dialogSource.Contains('"更新说明："')) `
    "the update result dialog does not render the manifest source"

Write-Host "PASS $passes update UI responsiveness source tests"
