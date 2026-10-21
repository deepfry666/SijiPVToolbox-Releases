# 历史静态下载站点工具

本目录保留早期自建静态站点的模板和兼容性测试，不再用于正式在线更新。正式客户端只读取
GitHub Release 的 `latest/download/stable.json`，正式发行由
`scripts/update-service/New-UpdateRelease.ps1` 和 `Publish-UpdateRelease.ps1` 完成。

历史脚本可把一个已验收的完整安装器转换成静态站点，其中包含：

- `index.html`：用户下载页；
- `stable.json`：插件读取的稳定版更新清单；
- `releases/<版本>/*.exe`：按版本固定、不可覆盖的完整安装器；
- `assets/site.css`：下载页样式。

无论清单来源如何，插件只负责下载并核对大小和 SHA-256，安装仍由用户在完全退出
AutoCAD 后运行完整 EXE 完成。
