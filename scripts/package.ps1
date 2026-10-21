[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# The legacy folder package could bypass the versioned EXE release path.
# Keep this historical entry point present, but fail before any filesystem or
# AutoCAD ApplicationPlugins operation can occur.
throw "Legacy package.ps1 is disabled. Use the versioned EXE produced by scripts/package-installer.ps1; no files were changed."
