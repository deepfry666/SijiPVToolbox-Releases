# 四季光伏工具箱 v0.3.2 AutoCAD 2020 CoreConsole验证记录

- 文档版本：`v0.3.2-test-final.1`
- 验证日期：2026-08-26
- 目标版本：`v0.3.2 TEST_UNSIGNED` / 程序集`0.3.2.0`
- 核心结论：AutoCAD 2020 Win64 Series`R23.1`真实数据库运行已验证
- 最终同哈希发布复跑：通过；`work/verification-v0.3.2/runs/20260826-050504`
- 验证边界：不等于完整GUI、探索者、系统级升级或其他AutoCAD版本验证

## 1. 结论边界

当前能够确认的是：`v0.3.2`代码已在 AutoCAD 2020 R23.1 CoreConsole真实DWG数据库
中执行，并通过新增的旋转外部障碍、旋转前序标注组和完整回溯车道夹具。只有
AutoCAD 2020 R23.1可以标为“核心运行已验证”。

冻结发布已使用`-SkipReleaseBuild`对最终候选中完全相同的DLL复跑，并核对
source/staged/实际加载DLL的SHA-256和MVID。环境清单记录
`ReleaseBuildSkipped=true`；实际加载DLL SHA-256为
`52F978AEFBF3E3EC2BD6D59D9DCB0011F9959888D5EA377794C87EF6FE05F18B`，MVID为
`a34e9aa6-ede1-4771-88f7-e1de46cb7057`。

完整AutoCAD GUI、Ribbon、PaletteSet、探索者代理对象和本机系统级
`v0.2.0 -> v0.3.2`升级均待用户保存图纸并关闭CAD后验收。

## 2. 验证环境

| 项目 | 当前已核对值 |
|---|---|
| 操作系统 | Windows 10专业版64位，10.0.19045 |
| AutoCAD注册产品 | AutoCAD 2020简体中文 |
| AutoCAD Series / Release | `R23.1` / `23.1.47.0` |
| AutoCAD安装目录 | `D:\cad\AutoCAD 2020` |
| `accoreconsole.exe` | `23.1.47.0.0`，Authenticode `Valid` |
| 托管API | `acmgd.dll`、`acdbmgd.dll`、`accoremgd.dll`均为`23.1.47.0.0`且签名`Valid` |
| `acad.exe` | `R23.1.47.0.0`，Authenticode `HashMismatch` |

本机GUI窗口属于探索者环境，`acad.exe`签名为`HashMismatch`，不能作为干净原版
AutoCAD 2020 GUI证据。CoreConsole及三个托管API签名有效；本记录只声明Core Engine
数据库运行通过。

验证脚本使用`/isolate`独立配置和显式`NETLOAD`，不依赖正在运行的GUI进程已加载
哪些插件。

## 3. 验证方法

最终冻结复跑命令：

```powershell
pwsh -File .\scripts\verify-autocad2020.ps1 `
  -AutoCADDir "D:\cad\AutoCAD 2020" -SkipReleaseBuild
```

约束如下：

1. 根目录模板只读，先复制到新的`work/verification-v0.3.2/runs/<时间戳>/`。
2. `-SkipReleaseBuild`锁定已打包候选payload，只构建`INTERNAL_VERIFY`程序集。
3. 项目根目录临时映射为ASCII路径`Q:`，规避AutoCAD 2020脚本对中文路径的解码问题。
4. 只有CoreConsole退出码为0、日志恰有一个`STVERIFY-DONE`且不存在
   `STVERIFY-FAIL`才通过。
5. 实际加载`Assembly.Location`、SHA-256和MVID必须与环境清单、候选build-result及
   source/staged payload一致。
6. CoreConsole本体必须是匹配R23.1的Autodesk有效签名文件。

不能只依赖CoreConsole退出码：`NETLOAD`失败或命令未知时CoreConsole也可能返回0。

## 4. v0.3.2新增数据库断言

| 检查项 | 断言 |
|---|---|
| 旋转外部障碍 | 障碍和候选尺寸按测量方向进入共同局部坐标；marker `ROTATED_EXTERNAL_FRAME=PASS` |
| 旋转前序组 | 前序标注完整几何登记到相关局部frame；marker `ROTATED_PRIOR_GROUP_FRAME=PASS` |
| 回溯陷阱 | 旧first-fit会失败的有限域由完整回溯找到解；重复运行车道和搜索统计一致；marker `LANE_BACKTRACK_SOLVER=PASS` |
| 同组局部坐标 | 37度平行尺寸按局部关键范围判断；marker `ROTATED_SIBLING_COLLISION=PASS` |
| 25项可解 | 25个互斥尺寸使用车道`0..24`，调整24项 |
| 第26项无解 | 25条车道内真实无解，整批回滚；marker `COLLISION_EXHAUSTION=PASS` |
| 回滚完整性 | 模型实体、工具XData、匿名尺寸块和临时碰撞图层恢复到测试前状态 |

预冻结运行`work/verification-v0.3.2/runs/20260826-043818/`用于建立新增夹具基线，
执行时`ReleaseBuildSkipped=false`，不作为最终发布证据。最终run
`work/verification-v0.3.2/runs/20260826-050504/`使用
`ReleaseBuildSkipped=true`，CoreConsole退出码为0，日志恰有一个`STVERIFY-DONE`且
没有`STVERIFY-FAIL`。最终marker包括：

```text
ROTATED_EXTERNAL_FRAME=PASS
ROTATED_PRIOR_GROUP_FRAME=PASS
LANE_BACKTRACK_SOLVER=PASS
COLLISION_EXHAUSTION=PASS
ADJUSTED_DIMENSIONS=24
MAX_LANE=24
```

## 5. 继承数据库断言

`v0.3.2`继续覆盖：

- 程序集版本和`JJJJ`、`STPVRAIL`、`STPVRAILANNOTATE`命令面；
- 原生`AlignedDimension`和`RotatedDimension`实体；
- 每个规划尺寸的实际`Measurement`和`Dimlfac=1`；
- 非注释性、`Dimtmove=0`及300/600模型空间文字高度；
- `FOURJTOOLS`、版本`0.3.2`和`META_V3` XData；
- 导轨与标注RunId分离、多来源、跨岛、对象选择及原子重画；
- 实际数据库读取工具导轨、Line和开放轻量多段线；
- 水平25/26项碰撞与37度局部坐标碰撞；
- 事务回滚后实体、XData、匿名尺寸块和测试图层零残留。

## 6. 确定性求解器断言

`tests/DeterministicLaneSolverTests.cs`定义11类独立用例：

- 复现旧first-fit陷阱并取得正确解；
- 相同输入重复运行得到相同车道、节点数和回溯数；
- 没有访问预算时仍能从错误分支回溯；
- 25项完全互斥问题确定使用`0..24`；
- 26项完全互斥问题判为无解；
- 矩阵域、回调域和跨车道约束；
- 冲突回调按无序候选对处理；
- 搜索失败返回空车道；
- 空问题、单车道和非法参数边界。

完整`scripts/test.ps1`共305项通过，终态标记为`PURE_CODE_REGRESSION=PASS`；日志位于
`work/release-v0.3.2/20260826-050316/01-pure-code-regression.log`，SHA-256为
`E4B7C57D6BF70AB5229EA5DE643DBF1E9E78999E704307E108F4F53C51070ED9`。

## 7. 几何与性能边界

- “共同局部坐标”是按标注测量方向建立，不等于建立屋面的语义模型。
- 支持的方向几何会按实际或拆解几何投影；未知对象可能退化为转换后的粗范围，结果
  仍可能保守，但不会静默提交已判定的重叠。
- 求解器完整搜索配置的有限车道域，不声明全局最少车道或最小总外移优化。
- 车道仍为`0..24`；真实无解或几何无法可靠读取时整批停止并回滚。
- 距屋面边、女儿墙、轴线和底图轮廓的定位仍需人工标注。

## 8. 最终证据

| 文件或对象 | 最终值 |
|---|---|
| 最终验证run目录 | `work/verification-v0.3.2/runs/20260826-050504` |
| 冻结source/staged/实际加载DLL SHA-256 | `52F978AEFBF3E3EC2BD6D59D9DCB0011F9959888D5EA377794C87EF6FE05F18B` |
| 实际加载MVID | `a34e9aa6-ede1-4771-88f7-e1de46cb7057` |
| `StructuralToolbox.Verify.dll` SHA-256 | `7E22273B6B614B618CE7EC2738AF77453438588233266B50D503DBBEE2E2496C` |
| 输入DWG副本（运行前）SHA-256 | `1566F194258D8AEE5C83B967985602AF3C46E59BDCE073C6B98D7509E0DB7254` |
| 输出DWG副本SHA-256 | `86B0CAEF3F3FEE17ADB04D0B374BBED16B475F55D91BBFE897C3AD714A926358` |
| CoreConsole日志SHA-256 | `CCC3AB036D26BDCA1880D59239BC032234FF3C304AF56F42AAD9BFD60ED36AF1` |
| 环境清单SHA-256 | `878C58BFAB4A483BAED731716DCDCE69DB16AE13A41E5E7F68828B58255BD075` |
| `result.json` SHA-256 | `1273D2F21D9F5E497A92F995C4E4579653C83845922B8B8F26169C76CEC236EE` |
| 纯代码回归日志SHA-256 | `E4B7C57D6BF70AB5229EA5DE643DBF1E9E78999E704307E108F4F53C51070ED9` |

## 9. 仍需GUI和外部验收

1. 首页、PaletteSet和Ribbon首次显示完整，100%/125%/150% DPI无截断。
2. 未提交预览、确认、全部换边、单组换边、只保留导轨、取消和`UNDO`。
3. 复杂屋面旋转尺寸、外部障碍和多组前序尺寸的实际视觉效果。
4. 原生尺寸样板继承及探索者代理对象的明确降级提示。
5. 用户关闭CAD后执行系统级`v0.2.0 -> v0.3.2`管理员升级，并验证最终EXE自身。
6. 尽可能干净的原版AutoCAD 2020和探索者环境分别验收。
7. AutoCAD 2018-2019、2021-2026在实机或VM通过前继续标为候选。
