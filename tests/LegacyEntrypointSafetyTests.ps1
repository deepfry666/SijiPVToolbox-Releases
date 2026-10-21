[CmdletBinding()]
param(
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

function Get-TreeFingerprint([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return "<absent>"
    }
    $root = [IO.Path]::GetFullPath($Path).TrimEnd("\")
    $items = @(Get-ChildItem -LiteralPath $root -Recurse -Force |
        ForEach-Object {
            $relative = $_.FullName.Substring($root.Length).TrimStart("\")
            if ($_.PSIsContainer) {
                "D|$relative"
            } else {
                $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                "F|$relative|$($_.Length)|$hash"
            }
        } | Sort-Object)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString(
            $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes(
                ($items -join "`n")))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-ProtectedTreeFingerprints([string[]]$Paths) {
    $result = @{}
    foreach ($path in $Paths) {
        $result[$path] = Get-TreeFingerprint $path
    }
    return $result
}

function Assert-ProtectedTreesUnchanged(
    [hashtable]$Before,
    [string[]]$Paths,
    [string]$Context
) {
    foreach ($path in $Paths) {
        Assert-Condition ((Get-TreeFingerprint $path) -eq $Before[$path]) `
            "$Context leaves the protected tree unchanged: $path"
    }
}

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    Write-Host "PASS: $Message"
}

function Invoke-ExpectedFailure([string]$Path, [string[]]$Arguments) {
    $pwsh = (Get-Command pwsh.exe -ErrorAction SilentlyContinue)
    if ($null -eq $pwsh) {
        $pwsh = (Get-Command powershell.exe -ErrorAction Stop)
    }
    $processArguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        ('"{0}"' -f $Path.Replace('"', '\"'))
    )
    foreach ($argument in $Arguments) {
        $processArguments += ('"{0}"' -f $argument.Replace('"', '\"'))
    }
    $process = Start-Process -FilePath $pwsh.Source `
        -ArgumentList $processArguments -WindowStyle Hidden -Wait -PassThru
    return [int]$process.ExitCode
}

$legacyScripts = @(
    (Join-Path $ProjectRoot "scripts\package.ps1"),
    (Join-Path $ProjectRoot "scripts\install.ps1"),
    (Join-Path $ProjectRoot "scripts\uninstall.ps1")
)
$legacyCommands = @(Get-ChildItem -LiteralPath `
    (Join-Path $ProjectRoot "scripts") -Filter "*.cmd" -File |
    Sort-Object FullName | ForEach-Object { $_.FullName })
Assert-Condition ($legacyCommands.Count -eq 4) `
    "all four legacy command wrappers are present"
$fingerprintPaths = @(
    (Join-Path $ProjectRoot "outputs"),
    (Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\ZhStructureToolbox.bundle"),
    (Join-Path $env:ProgramFiles "Autodesk\ApplicationPlugins\ZhStructureToolbox.bundle")
)

foreach ($path in $legacyScripts) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        "legacy script exists: $path"
    $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    Assert-Condition ($text -match '(?m)^throw\s+') `
        "legacy script fails closed before execution: $path"
    Assert-Condition ($text -notmatch '(?i)\b(Copy-Item|Move-Item|Remove-Item|New-Item|Start-Process|Set-Content)\b') `
        "legacy script has no filesystem/process mutation: $path"
    $before = Get-ProtectedTreeFingerprints $fingerprintPaths
    $exitCode = Invoke-ExpectedFailure $path @()
    Assert-Condition ($exitCode -ne 0) `
        "legacy script returns failure: $path"
    Assert-ProtectedTreesUnchanged $before $fingerprintPaths `
        "legacy script invocation ($path)"
}

foreach ($path in $legacyCommands) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        "legacy command exists: $path"
    $text = Get-Content -LiteralPath $path -Raw -Encoding Default
    Assert-Condition ($text -match '(?im)^exit\s+/b\s+1\s*$') `
        "legacy command returns failure: $path"
    Assert-Condition ($text -match '(?i)versioned.*EXE') `
        "legacy command directs users to the versioned EXE: $path"
    Assert-Condition ($text -notmatch '(?i)\b(powershell|pwsh|install\.ps1|uninstall\.ps1)\b') `
        "legacy command does not invoke a script fallback: $path"
    $before = Get-ProtectedTreeFingerprints $fingerprintPaths
    $process = Start-Process -FilePath $env:ComSpec `
        -ArgumentList @('/d', '/s', '/c', ('call "{0}"' -f $path)) `
        -WindowStyle Hidden -Wait -PassThru
    $exitCode = [int]$process.ExitCode
    Assert-Condition ($exitCode -ne 0) `
        "legacy command invocation is rejected: $path"
    Assert-ProtectedTreesUnchanged $before $fingerprintPaths `
        "direct PowerShell command invocation ($path)"
}

Write-Host "PASS legacy entrypoint safety tests"
