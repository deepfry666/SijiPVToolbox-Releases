# 公开源码与开发机迁移

## 唯一维护入口

[SijiPVToolbox-Releases](https://github.com/deepfry666/SijiPVToolbox-Releases) 同时维护源码、问题清单、安装包和自动更新。仓库名保留不变，避免影响已安装版本。

公开导入采用审查后的源码快照，基线记录在 `SOURCE_SNAPSHOT.json`。旧私有 Git 历史不在此仓库中；旧公开 Release、标签、安装包和校验文件保持原样。历史标签不指向本次导入的源码树，不应据此声称旧安装包已实现公开源码可复现构建。从后续新版本开始统一公开源码提交、版本标签和发行记录，不覆盖旧包。

## 公开范围与许可

- 自有源码按根目录 MIT 许可提供；第三方内容见 `THIRD_PARTY_NOTICES.md`。
- 工程 DWG/DXF、固定详图库、私人问题截图、历史 EXE/DLL、用户配置及私有开发历史不公开。
- C# 实现保持原基线；没有为仓库整理改动绘图算法、更新清单地址或安装身份。
- 历史文档中的工程名称已脱敏；历史验收记录是当时的记录，不是本轮重新运行的证据。未公开的图纸、截图、内部日志和构建回执仅保留在私人备份中，无法从公开仓库复核。
- 原始问题截图不随源码分发，`docs/issue-evidence/` 的历史引用仅作为附件索引，不表示文件可以公开下载。

## 在另一台电脑继续开发

1. 在旧开发目录检查 `git status`，将需要保留的未提交/未推送内容单独备份。旧库未归档时可以先同步；若已经归档，则在本地另存补丁、文件和 Git bundle，不向只读旧库推送。不要清空旧目录。
2. 在新的目录重新克隆公开主库：

```powershell
git clone https://github.com/deepfry666/SijiPVToolbox-Releases.git FourJ-PV-Toolbox
cd FourJ-PV-Toolbox
```

3. 对照备份逐项移植仍需保留的改动，只提交审查过的文件。不导入旧 `.git`，不要仅修改旧克隆的 `origin` 后推送全部历史或标签。
4. 确认工作内容和必要资源都已保留后，再将旧目录作为只读备份；不删除工程原图或本机设置。

## 构建与验证边界

正式构建需要 Windows、真实 AutoCAD 2020 Win64 R23.1、.NET Framework 编译器；安装包需要 Inno Setup。Autodesk SDK 及其二进制不由本仓库再分发。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -AutoCADDir "D:\cad\AutoCAD 2020"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
```

公开仓库不预置生成的 DLL、EXE 和私有 DWG。部分 CAD 验证脚本需要根目录 `模板图.dwg` 或显式 `-InputDwg`；请自行准备有权使用、且符合相应用例要求的图纸，测试只使用 `work/` 副本，不提交这些文件。支架大样是未集成到当前正式包的历史独立模块，其固定详图库也未公开；缺少资源时，相关功能和资源检查不能视为已通过。

历史追溯/发布检查中依赖旧源码提交、构建回执或私有原始产物的部分，需要原备份环境；公开快照没有携带这些历史证据。本次不修改这些检查以制造通过结果。

本次仅整理仓库，没有在 macOS 上完成 Windows / AutoCAD 正式构建、CAD 交互或安装升级验收，也没有生成、替换、发布任何新安装包。平台后续验收由 `ISSUE-0058` 跟踪。

## 更新入口保持不变

客户端继续读取：

```text
https://github.com/deepfry666/SijiPVToolbox-Releases/releases/latest/download/stable.json
```

保持 HTTPS、大小和 SHA-256 校验，不把仓库凭据放进客户端。发布新版本仍须完成现有候选构建、CAD 和安装器验证流程。
