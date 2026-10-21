$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path -Parent $PSScriptRoot
$assertions = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:assertions++
}

function Read-ProjectFile([string]$RelativePath) {
    $path = Join-Path $projectRoot $RelativePath
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        "file exists: $RelativePath"
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

$powerShellPath = if ($PSVersionTable.PSEdition -eq "Core") { (Get-Command pwsh -ErrorAction Stop).Source } else { (Get-Command powershell.exe -ErrorAction Stop).Source }
$readinessPath = Join-Path $projectRoot `
    "scripts\Test-ReleaseReadiness.ps1"
$readinessOutput = @(& $powerShellPath -NoProfile -ExecutionPolicy Bypass `
    -File $readinessPath -Version 1.0.8 -Phase Built 2>&1)
Assert-Condition ($LASTEXITCODE -eq 0) `
    "current six-module build passes release readiness"
$readinessText = $readinessOutput | Out-String
Assert-Condition ($readinessText.Contains("RELEASE_READINESS=PASS")) `
    "readiness emits a stable pass marker"
Assert-Condition ($readinessText.Contains(
    "built Bundle contains exactly the six approved release DLLs")) `
    "readiness checks the exact six-module Bundle"

$packager = Read-ProjectFile "scripts\package-installer.ps1"
Assert-Condition ($packager.Contains('[string]$Version')) `
    "generic packager accepts an explicit version"
Assert-Condition ($packager.Contains('release-v$requestedVersion3.json')) `
    "generic packager resolves a versioned release definition"
Assert-Condition ($packager.Contains(
    'docs\安装与兼容性说明_v$requestedVersion3.md')) `
    "generic packager resolves the matching install guide"
Assert-Condition (-not $packager.Contains('release-v0.3.9.json')) `
    "generic packager does not pin the previous release"
$artifactVerifier = Read-ProjectFile `
    "scripts\installer\Test-InstallerArtifacts.ps1"
Assert-Condition ($artifactVerifier.Contains(
    '"scripts\package-installer.ps1"')) `
    "installer artifact verification hashes the generic packager"
Assert-Condition (-not $artifactVerifier.Contains(
    'scripts\package-installer-v$releaseVersion.ps1')) `
    "installer artifact verification does not expect a copied packager"

$candidateBuilder = Read-ProjectFile "scripts\Build-ReleaseCandidate.ps1"
foreach ($required in @(
    "Test-ReleaseReadiness.ps1",
    "test.ps1",
    "build.ps1",
    "package-installer.ps1",
    "Test-InstallerArtifacts.ps1")) {
    Assert-Condition ($candidateBuilder.Contains($required)) `
        "candidate builder includes $required"
}
Assert-Condition ($candidateBuilder.Contains(
    'Candidate build never writes to outputs/')) `
    "candidate builder explicitly prevents implicit publication"
Assert-Condition ($candidateBuilder -notmatch
    'Invoke-ScriptStep\s+"[^"]*publish') `
    "candidate builder cannot publish a release"

$buildScript = Read-ProjectFile "scripts\build.ps1"
Assert-Condition ($buildScript.Contains('work\module-builds')) `
    "six-module build compiles under work before Bundle commit"
Assert-Condition ($buildScript.Contains('Assert-BundleFilesWritable')) `
    "six-module build checks every destination before Bundle commit"
Assert-Condition ($buildScript.Contains('Restore-CommittedBundle')) `
    "six-module build has rollback for a partial Bundle commit"
Assert-Condition ($buildScript.Contains(
    'Staged build must contain exactly the six release module DLLs')) `
    "six-module build rejects incomplete staging output"

$versionedWorkflowFiles = @(
    "scripts\New-ReleaseEvidence.ps1",
    "scripts\New-ReleaseArchive.ps1",
    "scripts\Publish-TestRelease.ps1",
    "scripts\verify-autocad2020.ps1",
    "scripts\verify-real-drawing.ps1",
    "scripts\build-real-drawing-verifier.ps1"
)
foreach ($file in $versionedWorkflowFiles) {
    $text = Read-ProjectFile $file
    Assert-Condition (-not $text.Contains("v0.3.9")) `
        "$file does not pin the v0.3.9 work directory"
    Assert-Condition (-not $text.Contains('"0.3.9.0"')) `
        "$file does not pin the v0.3.9 assembly version"
}

$realDrawingVerifier = Read-ProjectFile "scripts\verify-real-drawing.ps1"
Assert-Condition ($realDrawingVerifier.Contains(
    '[Reflection.AssemblyName]::GetAssemblyName($candidateVersionPath)')) `
    "real-drawing verification derives the candidate version"
$coreVerifier = Read-ProjectFile "scripts\verify-autocad2020.ps1"
Assert-Condition ($coreVerifier.Contains('[string]$Version')) `
    "CoreConsole verification accepts a release version"
Assert-Condition ($coreVerifier.Contains(
    'VerificationVersion = $version3')) `
    "CoreConsole evidence records the requested version"

$readinessScript = Read-ProjectFile "scripts\Test-ReleaseReadiness.ps1"
Assert-Condition ($readinessScript.Contains('$releaseStatusArguments = @(')) `
    "published readiness builds the Git pathspec as an argument array"
Assert-Condition ($readinessScript.Contains(
    '& $git.Source @releaseStatusArguments')) `
    "published readiness passes pathspec entries to Git without command parsing"
Assert-Condition ($readinessScript.Contains(
    '"src/StructuralToolbox.Update"')) `
    "published readiness names the approved release source roots explicitly"
Assert-Condition ($readinessScript.Contains(
    '"src/StructuralToolbox.DrawingTranslation"')) `
    "published readiness names the drawing translation source root explicitly"
Assert-Condition (-not $readinessScript.Contains(
    '"src", "scripts"')) `
    "published readiness does not absorb unrelated standalone source modules"
Assert-Condition ($readinessScript -notmatch '--\r?\n\s+src\s+scripts') `
    "published readiness cannot execute a pathspec as a PowerShell command"

$guide = Read-ProjectFile "docs\开发发布与软件更新手册.md"
foreach ($heading in @(
    "## 二、五个发布模块怎样协作",
    "## 三、一次正常迭代的完整路径",
    "## 四、用户怎样升级软件",
    "## 五、回滚方式",
    "## 六、在线更新的部署边界",
    "## 七、容易忽略但必须长期管理的事项")) {
    Assert-Condition ($guide.Contains($heading)) `
        "release guide contains $heading"
}

Write-Host "PASS $assertions release workflow tests"
