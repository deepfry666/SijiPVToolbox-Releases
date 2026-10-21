# 脚本入口

日常开发只需要记住三个入口：

| 目的 | 命令 |
|---|---|
| 完整回归 | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1` |
| 源码/构建体检 | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReleaseReadiness.ps1 -Version <版本> -Phase Source` |
| 候选构建 | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-ReleaseCandidate.ps1 -Version <版本> -AutoCADDir "D:\cad\AutoCAD 2020"` |

## 脚本分组

- `build*.ps1`：编译单个模块或完整六模块 Bundle；
- `test*.ps1`：单模块测试、图纸探针和完整回归；
- `verify*.ps1`：AutoCAD 2020/CoreConsole/真实图纸验证；
- `package*.ps1`：独立模块或安装器打包；
- `Build-ReleaseCandidate.ps1`：候选构建总入口，只写入 `work/`；
- `Publish-TestRelease.ps1`：验收后发布到 `outputs/`；
- `Test-ReleaseReadiness.ps1`：版本和交付文件体检；
- `Clear-ProjectWork.ps1`：只清理 `work/` 中未保护内容，默认先预览。

旧版 `package.ps1`、`install.ps1`、`uninstall.ps1` 和中文 `.cmd` 入口只用于安全拒绝和引导，不是开发入口。

所有脚本使用 UTF-8 BOM，以兼容 Windows PowerShell 5。脚本输出和构建临时物必须留在 `work/`，不要在根目录创建测试副本。
