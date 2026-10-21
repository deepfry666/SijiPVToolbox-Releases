# 四季光伏工具箱 v0.3.1 AutoCAD 2020 CoreConsole 验证记录

- 验证日期：2026-08-26
- 结论：AutoCAD 2020 Core Engine真实数据库验证通过
- 最终证据目录：`work/verification-v0.3.1/runs/20260826-035536`
- 后续复跑脚本：`scripts/verify-autocad2020.ps1`
- 验证边界：不等同于完整AutoCAD GUI、探索者UI、系统级升级或其他AutoCAD版本验证

## 1. 验证环境

| 项目 | 实际值 |
|---|---|
| 操作系统 | Windows 10专业版64位，10.0.19045 |
| AutoCAD注册产品 | AutoCAD 2020简体中文 |
| AutoCAD Series / Release | `R23.1` / `23.1.47.0` |
| 安装目录 | `D:\cad\AutoCAD 2020` |
| `accoreconsole.exe` | `23.1.47.0.0`，Authenticode `Valid` |
| `acad.exe` | `R23.1.47.0.0`，Authenticode `HashMismatch` |
| 托管API | `acmgd.dll`、`acdbmgd.dll`、`accoremgd.dll`均为`23.1.47.0.0`且签名`Valid` |
| PowerShell | `7.6.4` |

验证期间完整GUI进程窗口标题为
`TSDP2023 For Autodesk AutoCAD 2020 - [模板图.dwg]`。`acad.exe`签名状态为
`HashMismatch`，因此本机GUI不能作为“干净原版AutoCAD 2020”证据。
`accoreconsole.exe`及三个托管API签名有效；本记录只声明Core Engine数据库运行通过。

CoreConsole使用`/isolate`独立配置和显式`NETLOAD`，不依赖GUI进程已加载的插件。

## 2. 冻结验证流程

```powershell
pwsh -File .\scripts\test.ps1
pwsh -File .\scripts\package-installer.ps1 `
  -AllowUnsignedDevelopmentBuild -AutoCADDir "D:\cad\AutoCAD 2020"
pwsh -File .\scripts\package-installer.ps1 `
  -TestCurrentPayload `
  -FrozenBuildResult .\work\installer\release-candidate-v0.3.1\build-result.json
pwsh -File .\scripts\installer\Test-InstallerRuntime.ps1 `
  -BuildDirectory .\work\installer\test-v0.3.1
pwsh -File .\scripts\verify-autocad2020.ps1 `
  -AutoCADDir "D:\cad\AutoCAD 2020" -SkipReleaseBuild
```

关键约束：

1. 根目录模板先复制到新的`work/verification-v0.3.1/runs/<时间戳>/`，不覆盖原图。
2. `-SkipReleaseBuild`锁定已经打包的候选payload；只构建`INTERNAL_VERIFY` DLL。
3. 项目根目录临时映射为ASCII路径`Q:`，规避AutoCAD 2020脚本对中文路径的解码问题。
4. 只有CoreConsole退出码为0、恰有一个`STVERIFY-DONE`且没有
   `STVERIFY-FAIL`才通过。
5. 实际加载程序集的路径、SHA-256和MVID必须与source/staged payload、环境清单和
   build-result一致。

不能只根据CoreConsole退出码判断成功；早期基线已经证明，`NETLOAD`失败或命令未知时
CoreConsole仍可能返回0。

## 3. 数据库断言

| 检查项 | 最终断言 |
|---|---|
| 命令和版本 | 程序集`0.3.1.0`；存在`JJJJ`、`STPVRAIL`、`STPVRAILANNOTATE` |
| 原生尺寸 | 实际创建`AlignedDimension=6`、`RotatedDimension=2` |
| 实际测量 | `Measurement`逐ID匹配规划值；`Dimlfac=1` |
| 样式 | 非注释性，`Dimtmove=0`，模型空间文字高度300/600 |
| XData | `FOURJTOOLS`、版本`0.3.1`、`META_V3`完整 |
| 批次 | 导轨和标注RunId分离，多来源、跨岛、对象选择和重画原子性通过 |
| 实体读取 | 真实数据库读取4个有效实体：Line 2、LWPolyline 2；图层过滤1；反向2 |
| 水平同组碰撞 | 25个尺寸车道严格为`0..24`，调整数24 |
| 37度同组碰撞 | 3个尺寸车道严格为`0..2`，创建3、调整2、未解决0、ObjectId唯一 |
| 37度局部坐标判别 | 两条长平行尺寸在局部坐标中不重叠，调整数严格为0 |
| 耗尽回滚 | 第26个重合尺寸触发耗尽，整批零残留 |
| 事务回滚 | 模型实体、相关XData、匿名尺寸块和碰撞测试图层均恢复到测试前状态 |
| 实际加载 | `Assembly.Location`、SHA-256、MVID与冻结payload一致 |

最终成功标记：

```text
STVERIFY-DONE LAYER=<CoreConsole中文编码不可读> PANELS=1836 GROUPS=1 SEGMENTS=4 DIMSETS=4 DIMS=8 COLLISION_MOVES=2 SKIPPED=0 STYLE=PASS XDATA=META_V3 COMMANDS=PASS ROLLBACK=PASS ISLAND_BATCHES=PASS EXACT_GEOMETRY=PASS OBJECTIDS=PASS REDRAW_ATOMIC=PASS ALIGNED=6 ROTATED=2 RELEASE_SHA256=54CD377228C0EBF4795D65D782CC4DBC946D47F81E8380DA16381D8B45436E07 RELEASE_MVID=90c7d7a4-15e2-4f3e-b9f6-4c0a89d0d923 RELEASE_PATH="Q:\bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.dll" ENTITY_READER=PASS ENTITY_READS=4 LINE_READS=2 LWPOLYLINE_READS=2 FILTERED=1 REVERSED=2 INTERNAL_COLLISION=PASS ROTATED_SIBLING_COLLISION=PASS COLLISION_EXHAUSTION=PASS ADJUSTED_DIMENSIONS=24 MAX_LANE=24
```

## 4. 碰撞能力边界

`ROTATED_SIBLING_COLLISION=PASS`只声明旋转尺寸同组之间的
`LocalCritical`局部坐标分支已经执行并通过判别夹具。它不表示所有碰撞都已经转换到
局部坐标：

- 外部障碍及前序尺寸组仍使用世界坐标AABB，旋转屋面可能出现保守外移或假耗尽。
- 车道分配是确定性的first-fit，无回溯；理论上可能存在“有其他排列可解，但当前顺序
  耗尽”的情况。
- 这些限制不会造成重叠结果被静默提交；达到上限时整批回滚，但可能比最优布局更保守。

## 5. 37度夹具诊断记录

内部验证开发期间，runs
`033709`、`033834`、`033915`、`034043`、`034129`、`034150`、
`034223`均在测试代码事后读取`RotatedDimension.DimLinePoint`时由原生CoreConsole
退出，没有托管`STVERIFY-FAIL`。

这不是生产命令崩溃记录，而是内部夹具对未提交旋转尺寸做事后属性读取的问题。夹具随后
改为直接核验`AppendDimensions`返回的`CreatedCount`、调整数、未解决数、实际车道
序列和唯一ObjectId，并核对事务回滚。基础夹具在run `20260826-034309`通过，加入
局部坐标判别和图层回滚后在run `20260826-035232`通过；最终冻结payload的
`-SkipReleaseBuild`复验为run `20260826-035536`。

## 6. 纯代码回归

`scripts/test.ps1`最终共261项通过：

- 20项导轨布局
- 17项标注规划
- 13项已有导轨识别
- 10项几何指纹
- 10项XData codec
- 14项标注归属分类
- 10项设置
- 12项UI布局
- 55项更新安全静态门禁
- 94项发布追溯静态门禁
- 6项构建兼容性测试

日志：
`work/release-v0.3.1/20260826-035406/01-pure-code-regression.log`

终态标记：`PURE_CODE_REGRESSION=PASS`

## 7. 证据哈希

| 文件 | SHA-256 |
|---|---|
| 冻结source/staged/实际安装DLL | `54CD377228C0EBF4795D65D782CC4DBC946D47F81E8380DA16381D8B45436E07` |
| 候选schema 4 build-result | `922D6C6BA19F394C6F0343B3C50445D54A9DFB30353EFD9888289EE76EF77251` |
| `StructuralToolbox.Verify.dll` | `2AF914F140E7436156F739DE4EF440C13E8C6E2F2F4FDF9506629446C526F0F3` |
| 输入DWG副本（运行前） | `1566F194258D8AEE5C83B967985602AF3C46E59BDCE073C6B98D7509E0DB7254` |
| 输出DWG副本 | `E3AC16BF4E4318E8A25AB112503B871CC8F4C530FFB07EEE223D9F7171173FD1` |
| CoreConsole日志 | `3E61BA5B76722CA1F9B7276E0A5F29EFBD4308BFCC7037CAE66FFD511CB11B9D` |
| 环境清单 | `B8E8EC18513E11F13959731DFE96DC413DE23EA103341723DCC90E5363EF1D25` |
| `result.json` | `33CD48C32F48E2CDA41E9D97CB5F13CA8A5CBA03784E6C75B2C81C0B36603186` |
| 纯代码回归日志 | `CA8EB8E55698427D19B9CBB8B8B2EA566AAD7311DA1AB6D7AC8F9DB1732F2EC9` |

环境清单明确记录`ReleaseBuildSkipped=true`。实际加载MVID为
`90c7d7a4-15e2-4f3e-b9f6-4c0a89d0d923`。CoreConsole本体SHA-256为
`49571775E88DF066CDB3CF0A7A14E46C391BAD001D4FFD59C181EC2D1D10FC79`，
签名状态`Valid`。

## 8. 仍需GUI人工验证

1. 首页、PaletteSet、Ribbon首次显示完整，100%/125%/150% DPI无截断。
2. 未提交预览、确认、全部换边、单组换边、只保留导轨、取消和`UNDO`。
3. 复杂屋面的实际视觉避让及保守外移是否可接受。
4. 原生尺寸样板继承和探索者代理对象的明确降级提示。
5. 系统级`v0.2.0 -> v0.3.1`管理员升级及最终候选EXE自身运行。
6. 原版AutoCAD 2020和探索者环境分别验收；当前GUI环境不能作为干净原版证据。
7. AutoCAD 2018-2019、2021-2026在实机或VM通过前仍只能标为候选。
