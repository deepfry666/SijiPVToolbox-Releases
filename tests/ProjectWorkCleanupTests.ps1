$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path -Parent $PSScriptRoot
$cleanupScript = Join-Path $projectRoot "scripts\Clear-ProjectWork.ps1"
$sandboxRoot = Join-Path $projectRoot `
    ("work\project-work-cleanup-test-{0}" -f [guid]::NewGuid().ToString("N"))
$sandboxWork = Join-Path $sandboxRoot "work"
$assertions = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:assertions++
}

function Invoke-Cleanup([switch]$Apply) {
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
        $cleanupScript, "-ProjectRoot", $sandboxRoot)
    if ($Apply) {
        $arguments += "-Apply"
    }
    $powerShellPath = if ($PSVersionTable.PSEdition -eq "Core") { (Get-Command pwsh -ErrorAction Stop).Source } else { (Get-Command powershell.exe -ErrorAction Stop).Source }
    $output = @(& $powerShellPath @arguments 2>&1)
    Assert-Condition ($LASTEXITCODE -eq 0) "cleanup exits successfully"
    return $output | Out-String
}

try {
    $cleanupSource = Get-Content -LiteralPath $cleanupScript -Raw -Encoding UTF8
    Assert-Condition ($cleanupSource.Contains("ReparsePoint")) `
        "cleanup rejects links and junctions"

    $deleteDirectory = Join-Path $sandboxWork "delete-me\nested"
    $emptyDirectory = Join-Path $sandboxWork "empty-dir"
    $legacyDirectory = Join-Path $sandboxWork "legacy-output"
    $keepDirectory = Join-Path $sandboxWork "keep"
    New-Item -ItemType Directory -Path $deleteDirectory,$emptyDirectory,
        $legacyDirectory,$keepDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $deleteDirectory "temp.bin") `
        -Value "delete"
    Set-Content -LiteralPath (Join-Path $sandboxWork "top-level.tmp") `
        -Value "delete"
    Set-Content -LiteralPath (Join-Path $legacyDirectory "rollback.exe") `
        -Value "retain"
    Set-Content -LiteralPath (Join-Path $keepDirectory "manual.txt") `
        -Value "retain"

    $preview = Invoke-Cleanup
    Assert-Condition ($preview.Contains("WORK_CLEANUP=PREVIEW")) `
        "default mode is preview"
    Assert-Condition (Test-Path -LiteralPath $deleteDirectory) `
        "preview does not delete ordinary content"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $sandboxWork `
        "top-level.tmp")) "preview does not delete direct files"

    $applied = Invoke-Cleanup -Apply
    Assert-Condition ($applied.Contains("WORK_CLEANUP=APPLIED")) `
        "apply mode reports completion"
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $sandboxWork `
        "delete-me"))) "apply removes ordinary directories"
    Assert-Condition (-not (Test-Path -LiteralPath $emptyDirectory)) `
        "apply removes empty directories"
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $sandboxWork `
        "top-level.tmp"))) "apply removes direct files"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $legacyDirectory `
        "rollback.exe")) "legacy-output is protected"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $keepDirectory `
        "manual.txt")) "keep is protected"

    Write-Host "PASS $assertions project work cleanup tests"
} finally {
    if (Test-Path -LiteralPath $sandboxRoot) {
        Remove-Item -LiteralPath $sandboxRoot -Recurse -Force
    }
}
