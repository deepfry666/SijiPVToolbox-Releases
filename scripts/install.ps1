[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Installation is intentionally available only through the versioned EXE.
# Do not retain a script fallback that can write ApplicationPlugins directly.
throw "Legacy install.ps1 is disabled. Use the versioned EXE installer; no files were changed."
