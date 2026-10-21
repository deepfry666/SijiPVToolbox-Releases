# 四季光伏工具箱 v0.3.3 AutoCAD 2020 CoreConsole 验证记录

- 文档版本：`v0.3.3-verification.1`
- 目标版本：`v0.3.3 TEST_UNSIGNED` / 程序集 `0.3.3.0`
- 目标宿主：AutoCAD 2020 Win64 Series `R23.1`
- 记录日期：2026-08-26
- 当前结论：待最终 DLL 冻结后重跑；不得直接继承 v0.3.2 的哈希或 MVID

## 验证边界

只有 AutoCAD 2020 `R23.1` 有当前兼容矩阵允许的 payload。CoreConsole 证据只证明真实
数据库运行，不替代完整 GUI、Ribbon、探索者、安装器、系统级升级或其他 AutoCAD 版本
验收。候选版本没有 payload，不能用同一 DLL 推断兼容。

## 固定方法

```powershell
pwsh -File .\scripts\verify-autocad2020.ps1 `
  -AutoCADDir "D:\cad\AutoCAD 2020" -SkipReleaseBuild
```

运行必须复制根目录 DWG 到 `work/verification-v0.3.3/runs/<timestamp>/`，使用 ASCII 映射
路径，确认 CoreConsole 退出码为 0，日志恰有一个 `STVERIFY-DONE` 且没有
`STVERIFY-FAIL`。实际加载程序集的路径、SHA-256、MVID 必须同时匹配候选
`build-result.json`、Bundle staged 文件和环境记录。

## v0.3.3 必须包含的断言

- `JJJJ`、`STPVRAIL`、`STPVRAILANNOTATE` 命令和程序集版本 `0.3.3.0`；
- 导轨实际几何、原生尺寸、`Dimlfac=1`、300/600 模型空间高度；
- 旋转同组/外部障碍/前序标注组局部坐标碰撞；
- 确定性完整回溯、车道 `0..24`、第 26 项无解整批回滚，以及节点/约束预算耗尽的
  独立错误路径；
- 实际读取工具导轨、Line、开放轻量多段线以及 XData/RunId 隔离；
- 最终 DLL 与候选和实际加载文件的哈希、MVID 一致。

## 当前状态与禁止事项

当前并发诊断曾在第一次正常追加尺寸时触发 CoreConsole 失败；这份记录不把失败 run
标为通过，也不复用 v0.3.2 的验证数字。待导轨标注碰撞修复完成后，必须从纯代码回归
开始重新构建、打包、CoreConsole 复跑，并把实际 run 路径和哈希填入最终发布证据。

## 复杂实图验证边界

复杂实图另由 `scripts/verify-real-drawing.ps1 -Mode Full` 执行，固定基线为 3520 个
组件、1760 条导轨、842 个标注组、2326 个尺寸。为隔离正式程序集并取得命令结束时
快照，该步骤 NETLOAD 的是同一冻结源码编译的内部
`FourJTools.RealDrawingVerify.dll`。正式 `StructuralToolbox.dll` 不在该步骤执行 CAD
命令，只校验版本、SHA-256、MVID、build-result、SDK 和源码指纹完全绑定。最终文档和
证据必须明确 `formalCandidateExecuted=false`，不能扩大验证结论。
