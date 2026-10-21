[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",

    [string]$IsccPath,

    [switch]$RunInstallerRuntimeTest
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
try {
    $parsedVersion = [version]$Version
} catch {
    throw "Version is invalid: $Version"
}
$version3 = $parsedVersion.ToString(3)
$releaseDefinitionPath = Join-Path $PSScriptRoot `
    "installer\release-v$version3.json"
if (-not (Test-Path -LiteralPath $releaseDefinitionPath -PathType Leaf)) {
    throw "Release definition was not found: $releaseDefinitionPath"
}
$release = Get-Content -LiteralPath $releaseDefinitionPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([string]$release.releaseVersion -ne $version3) {
    throw "Release definition version does not match v$version3."
}
if ([string]$release.authenticodeExpectation -ne "NotSigned" -or
    [string]$release.currentReleaseClassification -ne "UNSIGNED") {
    throw "This candidate builder currently supports the controlled UNSIGNED pipeline only. Use the packager directly with the approved signing arguments for a signed release."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runRoot = Join-Path $projectRoot "work\release-v$version3\runs\$timestamp"
$logRoot = Join-Path $runRoot "logs"
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$powerShellPath = if ($PSVersionTable.PSEdition -eq "Core") { (Get-Command pwsh -ErrorAction Stop).Source } else { (Get-Command powershell.exe -ErrorAction Stop).Source }

function Invoke-ScriptStep([string]$Name, [string]$ScriptPath,
    [string[]]$Arguments) {
    if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
        throw "$Name script was not found: $ScriptPath"
    }
    $logPath = Join-Path $script:logRoot ($Name + ".log")
    Write-Host ""
    Write-Host "=== $Name ==="
    $output = @(& $script:powerShellPath -NoProfile -ExecutionPolicy Bypass `
        -File $ScriptPath @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    $output | Out-File -LiteralPath $logPath -Encoding utf8
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode. Log: $logPath"
    }
    return $logPath
}

$readinessScript = Join-Path $PSScriptRoot "Test-ReleaseReadiness.ps1"
$testScript = Join-Path $PSScriptRoot "test.ps1"
$buildScript = Join-Path $PSScriptRoot "build.ps1"
$packager = Join-Path $PSScriptRoot "package-installer.ps1"
$artifactVerifier = Join-Path $PSScriptRoot `
    "installer\Test-InstallerArtifacts.ps1"
$runtimeVerifier = Join-Path $PSScriptRoot `
    "installer\Test-InstallerRuntime.ps1"

$sourceReadinessLog = Invoke-ScriptStep "01-source-readiness" `
    $readinessScript @("-Version", $version3, "-Phase", "Source")
$regressionLog = Invoke-ScriptStep "02-pure-code-regression" $testScript @()
$buildLog = Invoke-ScriptStep "03-six-module-build" $buildScript @(
    "-AutoCADDir", $AutoCADDir)
$builtReadinessLog = Invoke-ScriptStep "04-built-readiness" `
    $readinessScript @("-Version", $version3, "-Phase", "Built")

$candidateDirectory = Join-Path $projectRoot `
    "work\installer\release-candidate-v$version3"
$packageArguments = @(
    "-Version", $version3,
    "-AutoCADDir", $AutoCADDir,
    "-OutputDirectory", $candidateDirectory,
    "-AllowUnsignedDistributionBuild"
)
if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
    $packageArguments += @("-IsccPath", $IsccPath)
}
$packageLog = Invoke-ScriptStep "05-package-candidate" `
    $packager $packageArguments
$candidateBuildResult = Join-Path $candidateDirectory "build-result.json"
if (-not (Test-Path -LiteralPath $candidateBuildResult -PathType Leaf)) {
    throw "Candidate build result was not generated: $candidateBuildResult"
}
$artifactLog = Invoke-ScriptStep "06-installer-artifacts" `
    $artifactVerifier @("-BuildDirectory", $candidateDirectory)

$runtimeArtifactLog = $null
$runtimeLog = $null
if ($RunInstallerRuntimeTest) {
    $testDirectory = Join-Path $projectRoot "work\installer\test-v$version3"
    $testPackageArguments = @(
        "-Version", $version3,
        "-AutoCADDir", $AutoCADDir,
        "-OutputDirectory", $testDirectory,
        "-TestCurrentPayload",
        "-FrozenBuildResult", $candidateBuildResult
    )
    if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
        $testPackageArguments += @("-IsccPath", $IsccPath)
    }
    $null = Invoke-ScriptStep "07-package-runtime-test" `
        $packager $testPackageArguments
    $runtimeArtifactLog = Invoke-ScriptStep "08-runtime-test-artifacts" `
        $artifactVerifier @("-BuildDirectory", $testDirectory)
    $runtimeLog = Invoke-ScriptStep "09-installer-runtime" `
        $runtimeVerifier @("-BuildDirectory", $testDirectory)
}

$candidate = Get-Content -LiteralPath $candidateBuildResult -Raw -Encoding UTF8 |
    ConvertFrom-Json
$summary = [ordered]@{
    schemaVersion = 1
    version = $version3
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    releaseClassification = [string]$release.currentReleaseClassification
    candidateBuildResult = $candidateBuildResult
    candidateBuildResultSha256 = (Get-FileHash -LiteralPath `
        $candidateBuildResult -Algorithm SHA256).Hash
    installer = [string]$candidate.installer
    installerSha256 = [string]$candidate.installerSha256
    installerRuntimeTested = [bool]$RunInstallerRuntimeTest
    logs = [ordered]@{
        sourceReadiness = $sourceReadinessLog
        pureCodeRegression = $regressionLog
        sixModuleBuild = $buildLog
        builtReadiness = $builtReadinessLog
        packageCandidate = $packageLog
        installerArtifacts = $artifactLog
        runtimeArtifacts = $runtimeArtifactLog
        installerRuntime = $runtimeLog
    }
    publication = [ordered]@{
        performed = $false
        reason = "Candidate build never writes to outputs/. Publish only after CAD and manual acceptance."
    }
}
$summaryPath = Join-Path $runRoot "candidate-summary.json"
$summary | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ""
Write-Host "RELEASE_CANDIDATE_BUILD=PASS"
Write-Host "Candidate: $($candidate.installer)"
Write-Host "SHA-256: $($candidate.installerSha256)"
Write-Host "Summary: $summaryPath"
Write-Host "Not published: run CAD/module acceptance before Publish-TestRelease.ps1."
