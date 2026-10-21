[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Uninstallation must use Inno's ownership-aware uninstaller. A legacy script
# cannot safely distinguish this product's Bundle from a same-name directory.
throw "Legacy uninstall.ps1 is disabled. Use the versioned EXE uninstaller; no files were changed."
