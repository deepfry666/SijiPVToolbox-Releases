# 当前开发入口

这里是当前版本的唯一开发导航。历史版本记录仍保留在 `docs/` 根目录，不能作为新功能的默认实现依据。

## 先看什么

1. [开发入口与工作流](开发入口.md)：从改代码到测试、候选构建和发布。
   - [Git 与版本发行](Git发布规范.md)：私有源码、公开下载、阿里云镜像。
2. [模块边界](../模块化架构说明_v0.3.9开发基线.md)：确认应该修改哪个项目。
3. [问题台账](../../ISSUES.md)：确认问题是否已有编号、是否等待验收。
4. [目录规范](../项目目录与临时文件管理规范.md)：确认源码、测试、临时文件和交付物落点。

## 当前正式模块

| 模块 | 源码 | 当前职责 |
|---|---|---|
| 壳 | `src/StructuralToolbox` | 首页、Palette、Ribbon、模块发现 |
| 契约 | `src/StructuralToolbox.Contracts` | 模块接口和导航契约 |
| 导轨 | `src/StructuralToolbox.Rail` | 自动画导轨、统计 |
| 配重 | `src/StructuralToolbox.Ballast` | 阵列识别、配重布置、统计 |
| 更新 | `src/StructuralToolbox.Update` | HTTPS 清单和完整安装包更新 |

自动排布 `StructuralToolbox.Layout` 已取消开发，源码只作历史保留，不接入正式包。图纸文字翻译 `StructuralToolbox.DrawingTranslation` 已恢复独立模块开发（ISSUE-0018），目前尚未接入正式包或翻译 API。

其他独立项目：`StructuralToolbox.PvComponent`、`StructuralToolbox.SupportDetail`、`StructuralToolbox.ArrayRecognition`、`StructuralToolbox.RailAnnotation`。接入主工具箱前必须完成独立测试和发布清单更新。

## 当前验证边界

- 正式运行目标：AutoCAD 2020 Win64，Series R23.1。
- 纯代码回归入口：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`。
- 所有真实 DWG 验证使用 `work/` 副本；根目录 DWG 不覆盖、不移动、不提交。
- `outputs/` 只放已经验收的当前交付包；候选构建全部写入 `work/`。
