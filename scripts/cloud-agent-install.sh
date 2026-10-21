#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for the Four Seasons PV Toolbox.
# Installs PowerShell 7, Mono, and the small Windows command shims that the
# pure-code regression scripts expect. AutoCAD 2020 itself is not installed.
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

if ! dpkg -s packages-microsoft-prod >/dev/null 2>&1; then
  deb="$(mktemp)"
  wget -q "https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb" -O "$deb"
  sudo dpkg -i "$deb"
  rm -f "$deb"
fi

sudo apt-get update -qq
sudo apt-get install -y \
  powershell \
  mono-devel \
  libmono-system-windows-forms4.0-cil \
  libmono-system-drawing4.0-cil \
  libmono-system-core4.0-cil \
  libmono-system-xml4.0-cil \
  libgdiplus \
  xvfb \
  ca-certificates \
  wget

sudo mkdir -p \
  /opt/sijipv/Windows/Microsoft.NET/Framework64/v4.0.30319 \
  /opt/sijipv/AppData \
  /opt/sijipv/ProgramFiles \
  /opt/microsoft/powershell/7

is_elf() {
  local magic
  magic="$(od -An -t x1 -N 4 "$1" 2>/dev/null | tr -d ' \n')"
  [[ "$magic" == "7f454c46" ]]
}

pwsh_home="/opt/microsoft/powershell/7"
if [[ -f "${pwsh_home}/pwsh" ]] && is_elf "${pwsh_home}/pwsh"; then
  sudo cp -a "${pwsh_home}/pwsh" "${pwsh_home}/pwsh.host"
fi
if [[ ! -x "${pwsh_home}/pwsh.host" ]]; then
  echo "PowerShell host binary is missing under ${pwsh_home}." >&2
  exit 1
fi
sudo ln -sfn pwsh.dll "${pwsh_home}/pwsh.host.dll"

sudo tee /opt/sijipv/csc-shim >/dev/null << 'EOF'
#!/bin/bash
# Translate a Windows csc.exe invocation to Mono's compiler and emit a
# directly executable launcher for /target:exe outputs.
set -euo pipefail
target="exe"
out=""
mcs_args=()
while [[ $# -gt 0 ]]; do
  arg="$1"
  shift
  case "$arg" in
    /nologo|-nologo|/utf8output|-utf8output)
      ;;
    /target:*|-target:*)
      target="${arg#*:}"
      mcs_args+=("/target:${target}")
      ;;
    /out:*|-out:*)
      out="${arg#*:}"
      out="${out//\\//}"
      ;;
    /reference:*|-reference:*|/r:*|-r:*)
      ref="${arg#*:}"
      ref="${ref//\\//}"
      mcs_args+=("/reference:${ref}")
      ;;
    /platform:*|-platform:*)
      ;;
    *)
      if [[ "$arg" == /* && "$arg" == *:* ]]; then
        mcs_args+=("$arg")
      else
        mcs_args+=("${arg//\\//}")
      fi
      ;;
  esac
done
if [[ -z "$out" ]]; then
  echo "csc-shim: /out: is required" >&2
  exit 1
fi
mkdir -p "$(dirname "$out")"
if [[ "$target" == "exe" || "$target" == "winexe" ]]; then
  assembly="${out}.mono"
  mcs /nologo "/out:${assembly}" "${mcs_args[@]}"
  cat > "$out" << LAUNCHER
#!/bin/sh
exec /usr/bin/mono "\$0.mono" "\$@"
LAUNCHER
  chmod +x "$out"
else
  mcs /nologo "/out:${out}" "${mcs_args[@]}"
fi
EOF
sudo chmod +x /opt/sijipv/csc-shim
sudo ln -sfn /opt/sijipv/csc-shim \
  /opt/sijipv/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe

sudo tee /opt/sijipv/cmd-shim >/dev/null << 'EOF'
#!/bin/bash
# Enough of cmd.exe /c for the legacy .cmd safety checks.
set -euo pipefail
command_line=""
seen_c=0
for arg in "$@"; do
  if [[ $seen_c -eq 1 ]]; then
    command_line+="$arg "
  fi
  if [[ "$arg" == "/c" || "$arg" == "/C" ]]; then
    seen_c=1
  fi
done
if [[ "$command_line" =~ call[[:space:]]+\"([^\"]+)\" ]]; then
  target="${BASH_REMATCH[1]}"
  if [[ -f "$target" ]] && grep -qiE 'exit[[:space:]]+/b[[:space:]]+1' "$target"; then
    exit 1
  fi
fi
exit 1
EOF
sudo chmod +x /opt/sijipv/cmd-shim

sudo tee /opt/sijipv/invoke-file.ps1 >/dev/null << 'EOF'
$ErrorActionPreference = "Stop"
if (-not (Get-PSDrive -Name C -ErrorAction SilentlyContinue)) {
    New-PSDrive -Name C -PSProvider FileSystem -Root "/opt/sijipv" -Scope Global | Out-Null
}
function global:Start-Process {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [Alias("PSPath")]
        [string]$FilePath,
        [Alias("Args")]
        [string[]]$ArgumentList,
        [switch]$Wait,
        [switch]$PassThru,
        [switch]$NoNewWindow,
        [string]$WorkingDirectory,
        [string]$Verb,
        [string]$WindowStyle,
        [string]$RedirectStandardOutput,
        [string]$RedirectStandardError,
        [string]$RedirectStandardInput
    )
    $bound = @{}
    foreach ($key in @(
            "FilePath", "ArgumentList", "Wait", "PassThru", "NoNewWindow",
            "WorkingDirectory", "Verb", "RedirectStandardOutput",
            "RedirectStandardError", "RedirectStandardInput")) {
        if ($PSBoundParameters.ContainsKey($key)) {
            $bound[$key] = $PSBoundParameters[$key]
        }
    }
    Microsoft.PowerShell.Management\Start-Process @bound
}
function Format-CommandArgument([string]$Value) {
    if ($Value -match '^[A-Za-z0-9_./:+@=-]+$') {
        return $Value
    }
    return "'" + ($Value -replace "'", "''") + "'"
}
$target = $env:SIJIPV_TARGET
if ([string]::IsNullOrWhiteSpace($target)) {
    throw "SIJIPV_TARGET is not set."
}
$argList = @()
if (-not [string]::IsNullOrEmpty($env:SIJIPV_ARGS_B64)) {
    $bytes = [Convert]::FromBase64String($env:SIJIPV_ARGS_B64)
    $text = [Text.Encoding]::UTF8.GetString($bytes)
    if (-not [string]::IsNullOrEmpty($text)) {
        $argList = @($text.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
    }
}
$pieces = @("& " + (Format-CommandArgument $target))
foreach ($argument in $argList) {
    $pieces += (Format-CommandArgument $argument)
}
Invoke-Expression ($pieces -join " ")
if (-not $?) { exit 1 }
exit 0
EOF

sudo tee /usr/local/bin/pwsh >/dev/null << 'EOF'
#!/bin/bash
set -euo pipefail
REAL="/opt/microsoft/powershell/7/pwsh.host"
export WINDIR="/opt/sijipv/Windows"
export APPDATA="${APPDATA:-/opt/sijipv/AppData}"
export ProgramFiles="${ProgramFiles:-/opt/sijipv/ProgramFiles}"
export DISPLAY="${DISPLAY:-:99}"
export ComSpec="${ComSpec:-/opt/sijipv/cmd-shim}"
if [[ ! -S /tmp/.X11-unix/X99 ]]; then
  mkdir -p /tmp/sijipv
  setsid Xvfb :99 -screen 0 1920x1080x24 -nolisten tcp \
    >/tmp/sijipv/xvfb.log 2>&1 </dev/null &
  for _ in $(seq 1 50); do
    if [[ -S /tmp/.X11-unix/X99 ]]; then
      break
    fi
    sleep 0.1
  done
fi
args=("$@")
script=""
rest=()
seen_file=0
for ((i=0; i<${#args[@]}; i++)); do
  a="${args[$i]}"
  if [[ "$a" == "-File" || "$a" == "-file" ]]; then
    seen_file=1
    script="${args[$((i+1))]:-}"
    script="${script%\"}"
    script="${script#\"}"
    rest=("${args[@]:$((i+2))}")
    break
  fi
done
if [[ $seen_file -eq 0 ]]; then
  exec "$REAL" "$@"
fi
if [[ -z "$script" ]]; then
  echo "pwsh: -File requires a script path" >&2
  exit 1
fi
if [[ "$script" != /* ]]; then
  script="$(pwd)/$script"
fi
export SIJIPV_TARGET="$script"
if [[ ${#rest[@]} -eq 0 ]]; then
  export SIJIPV_ARGS_B64=""
else
  export SIJIPV_ARGS_B64="$(printf '%s\0' "${rest[@]}" | base64 -w0)"
fi
exec "$REAL" -NoProfile -ExecutionPolicy Bypass -File /opt/sijipv/invoke-file.ps1
EOF
sudo chmod +x /usr/local/bin/pwsh
sudo cp /usr/local/bin/pwsh "${pwsh_home}/pwsh"
sudo cp /usr/local/bin/pwsh /usr/local/bin/powershell
sudo cp /usr/local/bin/pwsh /usr/local/bin/pwsh.exe
sudo cp /usr/local/bin/pwsh /usr/local/bin/powershell.exe
sudo chmod +x \
  "${pwsh_home}/pwsh" \
  /usr/local/bin/powershell \
  /usr/local/bin/pwsh.exe \
  /usr/local/bin/powershell.exe

sudo tee /etc/profile.d/sijipv-cloud.sh >/dev/null << 'EOF'
export WINDIR="${WINDIR:-/opt/sijipv/Windows}"
export APPDATA="${APPDATA:-/opt/sijipv/AppData}"
export ProgramFiles="${ProgramFiles:-/opt/sijipv/ProgramFiles}"
export DISPLAY="${DISPLAY:-:99}"
export ComSpec="${ComSpec:-/opt/sijipv/cmd-shim}"
EOF
sudo chmod 644 /etc/profile.d/sijipv-cloud.sh

# Keep a copy outside the git checkout. Environment builds of the default
# branch can run this copy before the setup pull request is merged.
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for name in cloud-agent-install.sh cloud-agent-start.sh; do
  src="${script_dir}/${name}"
  dest="/opt/sijipv/${name}"
  if [[ ! -f "$src" ]]; then
    echo "Missing setup script: $src" >&2
    exit 1
  fi
  if [[ "$(readlink -f "$src")" != "$(readlink -f "$dest" 2>/dev/null || true)" ]]; then
    sudo cp "$src" "$dest"
  fi
  sudo chmod +x "$dest"
done
