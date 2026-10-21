param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$DllPath = "",
    [int]$TimeoutSeconds = 300
)

# 翻译接口端到端验证（AutoCAD 2020 内核控制台）
#
# 能在这里验证的：
#   1. 交付 DLL 能在 AutoCAD 宿主进程里 NETLOAD；
#   2. STPVTRANSLATETEST 把两条示例文字发给当前配置的接口并拿回译文；
#   3. 整条链路（DNS、TLS、代理、密钥、模型、JSON 解析）在宿主进程里可用；
#   4. 自检不修改图纸，模板图.dwg 的哈希保持不变。
# 不能在这里验证的：
#   STPVTRANSLATECONFIRM 的命令体依赖 WinForms 对话框；STPVTRANSLATE
#   会直接写图，均不在接口自检脚本中执行，
#   内核控制台无法执行，必须在真实 AutoCAD 图形界面里验证。

$ErrorActionPreference = "Stop"
# 调用方可能把 PowerShell 7 的模块路径塞进 PSModulePath，让 5.1 找不到自带
# 模块（实测 Get-FileHash 会变成“无法识别的 cmdlet”）。只保留自带模块目录。
$windowsModules = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\Modules"
if (Test-Path -LiteralPath $windowsModules) { $env:PSModulePath = $windowsModules }

function Get-Sha256([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    try {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace("-", "") }
        finally { $sha.Dispose() }
    } finally {
        $stream.Dispose()
    }
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$logPath = Join-Path $env:LOCALAPPDATA "FourSeasonsPVToolbox\DrawingTranslation.log"
$moduleName = "StructuralToolbox.DrawingTranslation.dll"
$templateName = (-join @([char]27169, [char]26495, [char]22270)) + ".dwg"

function Find-NewestDeliveryDll([string]$root) {
    $deliveryRoot = Join-Path $root "outputs"
    if (-not (Test-Path -LiteralPath $deliveryRoot -PathType Container)) { return "" }
    $folders = @(Get-ChildItem -LiteralPath $deliveryRoot -Directory -Filter "StructuralToolbox.DrawingTranslation_*" -ErrorAction SilentlyContinue)
    $best = ""
    $bestVersion = $null
    foreach ($folder in $folders) {
        $candidate = Join-Path $folder.FullName $moduleName
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $parsed = $null
        try {
            $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($candidate)
            if ($info -and $info.FileVersion) { $parsed = [Version]$info.FileVersion }
        } catch {
            $parsed = $null
        }
        if ($null -eq $parsed) { continue }
        if ($null -eq $bestVersion -or $parsed -gt $bestVersion) {
            $bestVersion = $parsed
            $best = $candidate
        }
    }
    return $best
}

if ([String]::IsNullOrWhiteSpace($DllPath)) { $DllPath = Find-NewestDeliveryDll $projectRoot }
if ([String]::IsNullOrWhiteSpace($DllPath)) { throw "outputs 下没有找到任何交付 DLL" }
$DllPath = [IO.Path]::GetFullPath($DllPath)
$templatePath = Join-Path $projectRoot $templateName
foreach ($required in @($coreConsole, $DllPath, $templatePath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "missing: $required" }
}

$dllVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($DllPath).FileVersion
$dllHash = (Get-Sha256 $DllPath)
Write-Output ("交付 DLL：" + $DllPath)
Write-Output ("版本：" + $dllVersion + "；SHA-256：" + $dllHash)

$existingQ = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdQ = $false
if ($null -eq $existingQ) {
    & subst.exe Q: $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "could not map Q:" }
    $createdQ = $true
} elseif (-not (Test-Path -LiteralPath "Q:\scripts")) {
    throw "Q: is mapped outside this project"
}
function Convert-ToQPath([string]$value) {
    $relative = $value.Substring($projectRoot.Length).TrimStart("\")
    return "Q:/" + $relative.Replace("\", "/")
}
# 用 Process 而不是 Start-Job 起内核控制台：这台机器内存很紧，
# 再开一个 PowerShell 作业会直接 OutOfMemoryException。
function Invoke-Bounded([string[]]$arguments, [int]$seconds, [string]$label) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $coreConsole
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.CreateNoWindow = $true
    $quoted = @()
    foreach ($argument in $arguments) {
        if ($argument -eq '' -or $argument -match '\s') { $quoted += ('"' + $argument + '"') } else { $quoted += $argument }
    }
    $info.Arguments = ($quoted -join ' ')
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $info
    $null = $process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($seconds * 1000)) {
        try { $process.Kill() } catch { }
        try { $process.WaitForExit(15000) | Out-Null } catch { }
        $process.Dispose()
        throw "$label 超时：accoreconsole 没有在 $seconds 秒内退出"
    }
    $process.WaitForExit()
    $text = $stdout.Result + $stderr.Result
    $process.Dispose()
    return $text.Replace([string][char]0, "")
}
function Get-LogLines() {
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { return @() }
    return @([IO.File]::ReadAllLines($logPath, (New-Object Text.UTF8Encoding($false))))
}
function Assert-Contains([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "$label 失败：没有找到「$needle」" }
    Write-Output "PASS $label"
}
function Assert-NotContains([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "$label 失败：不应该出现「$needle」" }
    Write-Output "PASS $label"
}
function Assert-True([bool]$condition, [string]$label) {
    if (-not $condition) { throw "$label 失败" }
    Write-Output "PASS $label"
}

$runRoot = Join-Path $projectRoot ("work\api-verify-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
try {
    $fixture = Join-Path $runRoot "fixture.dwg"
    Copy-Item -LiteralPath $templatePath -Destination $fixture
    $templateBefore = (Get-Sha256 $templatePath)

    $lines = @('(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)')
    $lines += '_.NETLOAD'
    $lines += (Convert-ToQPath $DllPath)
    $lines += 'STPVTRANSLATETEST'
    $lines += 'STPVTRANSLATEINFO'
    $lines += '_.QUIT'
    $scriptPath = Join-Path $runRoot "selftest.scr"
    [IO.File]::WriteAllLines($scriptPath, $lines, [Text.Encoding]::ASCII)

    $before = @(Get-LogLines).Count
    $arguments = @("/i", (Convert-ToQPath $fixture), "/s", (Convert-ToQPath $scriptPath), "/l", "chs", "/isolate", "STPV_API_SELFTEST", (Convert-ToQPath $runRoot))
    $output = Invoke-Bounded $arguments $TimeoutSeconds "STPVTRANSLATETEST"
    [IO.File]::WriteAllText((Join-Path $runRoot "selftest.console.log"), $output, (New-Object Text.UTF8Encoding($false)))
    $all = @(Get-LogLines)
    $new = @($all | Select-Object -Skip $before)
    $log = ($new -join [Environment]::NewLine)

    Assert-Contains $log "模块 NETLOAD：版本 $dllVersion" "宿主进程加载的就是这份交付 DLL"
    Assert-Contains $log "STPV_TRANSLATE_SELFTEST_PASS" "接口自检通过"
    Assert-Contains $log "samples=2" "自检送出 2 条示例文字"
    Assert-NotContains $log "STPV_TRANSLATE_SELFTEST_FAIL" "自检没有失败记录"

    $passLine = @($new | Where-Object { $_.Contains("STPV_TRANSLATE_SELFTEST_PASS") })
    Assert-True ($passLine.Count -eq 1) "只有一条自检通过记录"
    $first = ""
    if ($passLine.Count -eq 1) {
        $marker = "first="
        $index = $passLine[0].IndexOf($marker, [StringComparison]::Ordinal)
        if ($index -ge 0) {
            $rest = $passLine[0].Substring($index + $marker.Length)
            $stop = $rest.IndexOf(" ")
            if ($stop -ge 0) { $first = $rest.Substring(0, $stop) } else { $first = $rest }
        }
    }
    Assert-True ($first.Length -gt 0) "接口返回了非空译文"
    Assert-True ($first -ne "北立面支架") "返回的译文与原文字不同"
    Assert-True ($first -match "[A-Za-z]") "返回的译文含英文字母（模型确实翻译了）"
    Write-Output ("示例译文：" + $first)
    Assert-NotContains $log "缺少密钥" "接口配置里有可用密钥"
    Assert-NotContains $log "无法连接翻译服务" "宿主进程能连上翻译服务"

    $templateAfter = (Get-Sha256 $templatePath)
    Assert-True ($templateBefore -eq $templateAfter) "模板图.dwg 未被修改"
    Write-Output ("STPV_API_VERIFY_DIR=" + $runRoot)
    Write-Output "PASS 翻译接口端到端自检（真实接口 + AutoCAD 内核控制台）"
} finally {
    if ($createdQ) { & subst.exe Q: /D | Out-Null }
}
