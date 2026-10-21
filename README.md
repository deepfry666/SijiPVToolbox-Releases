# 四季光伏工具箱

面向 AutoCAD 2020 Win64 R23.1 的光伏结构绘图工具，提供自动画导轨、配重布置与统计、图纸文字翻译及软件更新。

**本仓库现在同时管理公开源码、安装包和自动更新。** 自有代码采用 [MIT](LICENSE)；第三方许可见 [声明](THIRD_PARTY_NOTICES.md)。工程图纸、私人配置、历史二进制和私有开发历史不随源码公开。

## 下载与使用

- [下载正式安装包与校验文件](https://github.com/deepfry666/SijiPVToolbox-Releases/releases/latest)
- 当前公开稳定版：`v1.0.8`。安装前保存图纸，完全退出 AutoCAD / 探索者，安装后重启 AutoCAD，输入 `JJJJ` 打开工具箱。
- 仅 AutoCAD 2020 Win64 R23.1 属于既有验证范围；不承诺其他 CAD、AutoCAD LT 或 macOS 兼容。
- 安装包尚未进行 Windows 代码签名，请确认来源并核对 SHA-256；校验值不能替代发布者身份签名。

## 开发入口

```powershell
git clone https://github.com/deepfry666/SijiPVToolbox-Releases.git FourJ-PV-Toolbox
cd FourJ-PV-Toolbox
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -AutoCADDir "D:\cad\AutoCAD 2020"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
```

正式构建需要 Windows、真实 AutoCAD 2020 和 .NET Framework；安装器还需要 Inno Setup。生成产物保存在忽略的本地目录，安装包通过 GitHub Releases 分发，不提交到 Git。

本次公开源码整理没有重新构建或发布安装包。部分 CAD 测试和历史支架独立模块需要自备有权使用的 DWG，历史追溯检查还可能需要原始备份；缺少资源和平台时不能宣称完整回归通过。

## 文档

- [公开范围、构建限制与另一台电脑迁移](OPEN_SOURCE.md)
- [当前开发工作流](docs/current/README.md) · [脚本入口](scripts/README.md)
- [功能与历史使用说明](docs/历史使用说明.md)
- [问题清单](ISSUES.md) · [版本记录](CHANGELOG.md)

旧 Release、标签和更新地址保持原样。旧标签不标识本次导入的源码快照；基线见 [SOURCE_SNAPSHOT.json](SOURCE_SNAPSHOT.json)。后续发行须使用新版本号并完成既有 CAD、安装和升级验证。
