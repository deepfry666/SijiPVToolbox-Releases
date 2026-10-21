# AutoCAD 2020 意外关闭修复记录（v0.3.3 开发中）

日期：2026-08-26

## 现象

- Autodesk 错误报告窗口提示“软件问题导致 AutoCAD 意外关闭”。
- 实际崩溃进程为后台验证使用的 `accoreconsole.exe`，不是用户前台
  `acad.exe`。
- 多次转储稳定出现 AutoCAD 原生访问冲突，故障发生在托管验证命令返回前后。

## 定位结论

- 主绘图事务提交、原生尺寸回读、跨阵列重画均能完成。
- 故障只在内部验证器连续创建多批原生 `Dimension`，随后依赖事务批量回滚时出现。
- 强制 `GC.Collect/WaitForPendingFinalizers` 不能修复，反而可直接触发故障。
- 临时颜色包装器的释放策略经 A/B 对照后排除为根因。
- 该压力路径不是工具正常绘图路径；正常绘图创建尺寸后提交事务。

## 修复

1. Core Console 脚本首先设置 `REPORTERROR=0`，即使未来验证失败也不再弹出
   Autodesk 错误报告窗口。
2. 主验证保留原生尺寸创建、事务提交、数据库回读、跨阵列重画和常规回滚验证。
3. 删除主流程中的“多批原生尺寸创建后整批回滚”压力调用。
4. 25 车道成功、26 项无解和搜索预算边界改由同源
   `DeterministicLaneSolver` 验证。
5. 成功标记明确写出
   `NATIVE_ROLLBACK_STRESS=SKIPPED_AUTOCAD2020`，不把跳过项伪装成通过。
6. 验证期间临时切换到 AutoCAD 原生配置；`finally` 中恢复用户原有
   `XCFrameV2_Automation` 配置，并记录切换前、切换中、恢复后的值。

## 复测证据

发布 DLL SHA-256：

`B2EA440EE6BFFB049DE8F318DD9B40080416A0A45FFF06A9392FBF97E8B5D212`

以下运行均得到 `STVERIFY-DONE`、Core Console 退出码 `0`，且没有错误报告窗口：

- `work/verification-v0.3.3/runs/20260826-101040`
- `work/verification-v0.3.3/runs/20260826-101129`
- `work/verification-v0.3.3/runs/20260826-101213`

源码门禁：`PASS 135 rail update safety source tests`。

## 边界

- 本次只解决后台验证导致的 AutoCAD 意外关闭，没有制作安装器、发布包或实图验收。
- 原生批量回滚压力夹具保留为未调用诊断代码；如以后恢复，必须拆到独立
  Core Console 进程，不能再次放入同一个验证命令连续执行。
