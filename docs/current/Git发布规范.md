# Git 与版本发行规范

源码与发行现在统一维护于 `deepfry666/SijiPVToolbox-Releases`。历史标签及安装包不改动，本次导入源码不伪装为旧标签对应构建；详见根目录 `OPEN_SOURCE.md`。生成的 EXE/DLL 不再提交 Git，而是作为候选本地验证后上传 Release。下一次正式发版使用新版本号，重新生成当前公开源码提交的追溯记录；依赖原私有提交或本地历史产物的检查仍需原备份环境。

## 版本身份

每次正式发行使用同一个三段版本号和同名 Git tag：

```text
版本：<主版本.次版本.补丁版本>
Tag：v<版本>
安装包：四季光伏工具箱_Setup_v<版本>.exe
GitHub 资产：FourJ_PV_Toolbox_Setup_v<版本>.exe
```

版本号、六个正式 DLL、Bundle manifest、安装器、Git tag、GitHub Release 和
`stable.json` 必须一致。禁止在同一个 tag 上覆盖不同文件。

## 唯一在线更新源

正式客户端只读取：

```text
https://github.com/deepfry666/SijiPVToolbox-Releases/releases/latest/download/stable.json
```

`stable.json` 的 `url` 必须直接指向同版本 GitHub Release 的安装器资产，`mirrors` 必须为空。
客户端仍会校验 HTTPS、文件大小和 SHA-256，并使用临时文件完成原子下载。阿里云 VPS 不再
参与清单检查、安装包下载或发行部署。

已发布的 `v1.0.3` 客户端仍内置 VPS 优先逻辑。它遇到可访问但过期的 VPS 清单时不会继续
读取 GitHub，因此用户需要手动安装一次包含 GitHub 单源逻辑的新版本；后续版本即可从
GitHub 在线发现和下载。

## 发布顺序

1. 完成代码、测试、版本号和版本文档。
2. 运行 `scripts/test.ps1` 和 `Test-ReleaseReadiness.ps1 -Phase Source`。
3. 运行 `Build-ReleaseCandidate.ps1`，候选只写入 `work/`。
4. 完成 AutoCAD 2020、安装器隔离运行和人工界面验收。
5. 提交候选对应源码和文档并推送远端。
6. 用 `New-UpdateRelease.ps1` 生成 GitHub Release 资产；该目录只包含安装器、
   `stable.json`、`sha256.txt`、`release.json` 和发布说明。
7. 用 `Publish-UpdateRelease.ps1` 创建并推送不可变 tag，上传草稿资产，下载回验全部资产后
   再公开 Release 并设为 latest。
8. 用旧版安装器检查更新、下载并校验；最后运行
   `Test-ReleaseReadiness.ps1 -Phase Published`。

## 回滚

保留上一稳定版本的 Git tag、GitHub Release、安装器和 SHA-256。新版本出现问题时，重新把
上一稳定 Release 设为 latest，或发布修复后的新补丁版本。不要覆盖、移动或删除已经公开的
tag 和版本资产。

## 常用命令

```powershell
# 准备 GitHub Release 资产
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/update-service/New-UpdateRelease.ps1 `
  -Version <版本> `
  -InstallerPath <已验证的完整安装器> `
  -SourceRef <已提交的源码提交> `
  -NotesPath <公开发布说明> `
  -ReleaseDate <发行日期>

# 发布 tag 和 GitHub Release，并下载回验
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/update-service/Publish-UpdateRelease.ps1 `
  -ReleaseDirectory outputs/update-release-v<版本>
```
