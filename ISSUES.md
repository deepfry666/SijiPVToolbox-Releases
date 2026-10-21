# 项目问题清单

- [ ] `ISSUE-0057` 将已审查的源码快照导入现有公开发行仓库，统一源码与更新维护。
  - 范围：补充 MIT 许可和维护说明；排除工程 DWG、历史构建产物与私人截图；不导入私有 Git 历史，不改更新地址、版本标签或已发布安装包。
  - 验收：公开文件集与密钥复核通过；代码及更新配置与基线差异可追溯；旧发行资产保持不变。

- [ ] `ISSUE-0058` 在具备 Windows / AutoCAD 2020 的环境验证公开快照的正式构建与资源准备流程。
  - 状态：待平台验证；本轮 macOS 仅做仓库整理，不发布新安装包。工程 DWG 与未确认授权的内置节点库不进入公开快照。
  - 验收：自备有权使用的测试图纸后执行相关 CAD 检查；完成公开源码的正式构建、安装及升级后配置保留验证，不用源码静态检查代替实机运行。

- [x] `ISSUE-0056` 本机升级辅助验证把会持续追加的翻译运行日志当作设置文件，导致 v1.0.8 安装成功后误判配置变化并回退。
  - 状态：已完成；日志作为可追加文件单独验证，七个实际配置/缓存仍严格校验。
  - 完成日期：2026-10-12。
  - 验证：实际 v1.0.8 升级完成，install-status.json 为 PASS；七个配置/缓存哈希不变，日志包含原备份的完整前缀，证据 work/install-v1.0.8/log-preservation.json。
  - 来源：2026-10-12 安装 v1.0.8 的后置校验确认；备份之后的 CAD 验证正常追加 DrawingTranslation.log。
  - 验收要求：对设置/缓存继续严格校验哈希，对运行日志保留原内容并单独识别为可追加文件；完成真实升级且七个配置/缓存文件不变，版本和六 DLL 匹配 v1.0.8。

- [x] `ISSUE-0055` 用户认为导轨标注效果不符合需求，要求全部撤下，并将翻译完成时的功能组合作为最新版本。
  - 状态：已完成；翻译完成基线已恢复，v1.0.8 为 GitHub latest 且已安装到本机。
  - 完成日期：2026-10-12。
  - 验证：翻译模块 15 文件与 4e098b0 一致，仅版本元数据变化；20 项标注退役回归、完整回归、六 DLL CAD 无标注类型/命令/资源、翻译替换/重开及安装器验证通过。GitHub 四资产下载回验一致；安装器 2245217 字节，SHA-256 为 4E28C12289ED004F74067757508B69ED97B12A945A877A960E4250B73726DC20。
  - 本机验证：系统安装及注册表均为 1.0.8，六 DLL 版本/哈希匹配发布候选，STPVRAILDIM/STPVRAILANNOTATE/GDBZ 均已撤下，七个设置/缓存文件不变；备份 work/install-v1.0.8/backup-1.0.7-20261012-161403，安装证据 work/install-v1.0.8/install-status.json。
  - 发布地址：https://github.com/deepfry666/SijiPVToolbox-Releases/releases/tag/v1.0.8；详细记录 docs/安装器验证记录_v1.0.8.md。
  - 来源：用户于 2026-10-12 明确要求“把导轨标注全部去掉，最新版本换成上次做完的翻译那个版本”；已自行退出 AutoCAD，同意继续安装。
  - 验收要求：主工具箱及独立交付不再提供导轨标注入口、命令、脚本资源或模块；翻译代码与完成基线一致，保留直接替换/确认后替换；完整回归、CAD 和安装器验证通过；GitHub latest 和本机安装均切换到新版本，用户设置及原始图纸不变。

- [x] `ISSUE-0054` 用户安装 v1.0.7 时，本机已有系统级 1.0.10，安装器因降级保护无法继续。
  - 状态：已完成；备份后通过原卸载器和正式安装器完成系统级切换。
  - 完成日期：2026-10-12。
  - 验证：安装器 SHA-256 与 GitHub v1.0.7 一致；原卸载器和新安装器均返回 0。系统 Bundle 为 1.0.7.0，六 DLL 的版本及哈希全部匹配候选；HKLM 卸载记录为 1.0.7；STPVRAILDIM、STPVTRANSLATE、STPVTRANSLATECONFIRM 均已注册。八个设置/配置/缓存文件哈希未变化。
  - 恢复备份：`work/install-v1.0.7/backup-1.0.10-20261012-155224`，含原 Bundle、安装元数据、卸载器、注册表及用户设置；23 个备份文件哈希校验通过。
  - 安装证据：`work/install-v1.0.7/install-status.json` 为 PASS；卸载及安装日志分别为 `uninstall-1.0.10.log`、`install-1.0.7.log`。无须重启 Windows；尚未启动 AutoCAD 做本轮人工交互验收。
  - 来源：用户于 2026-10-12 要求安装本轮版本并提供安装器截图。
  - 核查证据：系统 Bundle manifest、七个 DLL 和 HKLM 卸载记录均为 1.0.10；GitHub latest 为 v1.0.7。本机版本较高，截图所示拦截是正常降级保护。
  - 验收要求：保留可恢复的 1.0.10 安装及设置备份；通过原卸载器和已发布 v1.0.7 安装器完成同范围切换，不修改原始 DWG；安装清单、六 DLL 哈希和系统卸载记录全部匹配 v1.0.7，用户设置不变。

- [x] `ISSUE-0053` 正式发布脚本不接受已存在的翻译 preview10 独立交付目录，导致两个功能整合后无法生成正式交付目录。
  - 状态：已完成；仅允许翻译 preview 正整数编号目录。
  - 完成日期：2026-10-12。
  - 验证：25 项发布原子性回归通过，覆盖未知目录拒绝、翻译预览目录在失败回滚及成功发布后哈希不变；v1.0.7 正式输出成功，日志 `work/v107-local-publish.log`。
  - 来源：2026-10-12 执行 v1.0.7 的 `Publish-TestRelease.ps1` 时确认，错误为 unsupported entry `StructuralToolbox.DrawingTranslation_preview10_AutoCAD2020`。
  - 验收要求：严格允许已知翻译预览目录命名，发布、归档或回滚不得修改其文件；无关未知目录继续拒绝；发布原子性回归及实际 v1.0.7 正式输出通过。

- [ ] `ISSUE-0050` 将用户提供的导轨对齐标注插件 `导轨对齐标注_V1.0.lsp`（命令 `GDBZ`）接入四季光伏工具箱主界面，按现有工具箱样式提供首页入口、操作页和 Ribbon 按钮。
  - 原分支编号：v1.0.6 分支的编号已分别迁移为 ISSUE-0050/0051；以下保留当时记录。
  - 状态：已取消（2026-10-12）；用户明确要求全部移除导轨标注。保留历史记录，不再推进此功能，撤下工作由 ISSUE-0055 跟踪。
  - 2026-10-12 Linux 验证证据（mcs + mono/xvfb，仅为替代验证）：`ToolboxUiLayoutTests` 29 项通过（含导轨对齐标注页 100%/125%/150%/200% 与 300×280 紧凑宽度、首页按钮顺序、返回首页、无模块时不显示入口）；`RailDimensionScriptTests` 8 项通过（嵌入资源 SHA-256 与附件一致、释放复用与损坏重写、LISP 路径和加载表达式）；`RailDimensionSourceTests` 33、`RailUpdateSafetySourceTests` 30、`RailAnnotationSourceTests` 16、`ModuleIsolationSourceTests` 15、`ReleaseTraceabilitySourceTests` 378、`UpdateUiResponsivenessSourceTests` 8 项通过；四档 DPI 和紧凑宽度截图人工检查无裁切、重叠或横向滚动。
  - 未完成检查：Windows `csc` 正式构建与完整 `scripts/test.ps1`（`Built` 阶段要求先运行 `scripts/build.ps1` 并提交 `1.0.6.0` 的五个 DLL）；AutoCAD 2020 中首页按钮、Ribbon 按钮和 `STPVRAILDIM` 运行 `GDBZ` 的预览、确认、取消和 `UNDO`。
  - 来源：用户于 2026-10-12 提供 `导轨对齐标注_V1.0.lsp` 及使用说明，要求接入工具箱并以 `v1.0.6` 走现有在线更新发布。与已取消的 `ISSUE-0015`（`v0.1.0` 独立方案，阵列净距 `200 mm`）不同，本次以用户 V1.0 脚本为准：阵列净距 `0～25 mm`、可选跨阵列标注和取消相同标注、次导轨固定 `700 mm` 偏移、跨导轨垂距超过 `3000 mm` 不跨接、首选 `TSSD_100_100` 标注样式。
  - 用户附件：LSP 为 GBK 编码、LF 换行，SHA-256 `ACDA16C65A5430E79740B599AB7A40432C8B20C5FB6BD6075AA313EC48AEF2BC`；使用说明 SHA-256 `9AAA74AD5B98C084EB6CB0F81DA8D2C00141792E8EEC3AF23F87DF8071ACC753`。
  - 定版方案：脚本逐字节原样嵌入正式 `StructuralToolbox.Rail.dll`，不新增第六个 DLL、不改变 Bundle 和安装器文件集；新增工具箱命令 `STPVRAILDIM`，运行时把脚本释放到 `%LOCALAPPDATA%\StructuralToolbox\rail-dimension\V1.0\`，校验 SHA-256 后 `load` 并执行 `GDBZ`。`%APPDATA%\StructuralToolbox` 中的用户设置不读写。`STPVRAIL` 画轨流程保持只生成导轨，不生成、修改或删除尺寸；已退役的 `STPVRAILANNOTATE` 不恢复。
  - 验收要求：首页、导轨对齐标注页和 Ribbon 使用现有中文命名与布局风格；规则示意图标签与页面规则名称一致，并区分组件外轮廓净距与导轨中心线偏移；页面在 100%、125%、150%、200% DPI 及紧凑宽度下无裁切、重叠或横向滚动；嵌入脚本与用户附件 SHA-256 一致；完整 `scripts/test.ps1` 通过；AutoCAD 2020 中通过首页按钮、Ribbon 按钮和 `STPVRAILDIM` 运行 `GDBZ` 的预览、确认、取消和一次 `UNDO` 验证。

- [x] `ISSUE-0051` 以 `v1.0.6` 通过现有 GitHub Release 发布链发布包含导轨对齐标注的安装包，使已安装 `v1.0.5` 可在线检查并更新。
  - 状态：已完成；下方为原云端准备记录，后续 Windows 发布已核实。
  - 完成日期：2026-10-12。
  - 最终验证：GitHub v1.0.6 已公开，源码 tag 为 a570929；安装器、stable.json、sha256.txt、release.json 下载回验一致。核查日志和资产位于 `work/github-latest-audit-20261012`，此版即本次合并的标注基线。
  - 2026-10-12 已完成：全部版本号改为 `1.0.6` / `1.0.6.0`，新增 `scripts/installer/release-v1.0.6.json`（GUID 与 `v1.0.5` 一致）、`CHANGELOG.md` 的 `v1.0.6` 节、`docs/current/release-notes-v1.0.6.md` 和 `v1.0.6` 安装、实施、验证记录；Linux 上 `Test-ReleaseReadiness.ps1 -Version 1.0.6 -Phase Source` 的通过项与阻塞项与 `v1.0.5` 基线一致，阻塞项只来自缺少 Windows/AutoCAD/构建工具。
  - 2026-10-12 用户确认由 JJ 的 Windows 机器构建发布；云端代理复查后仍无法执行：`list-self-hosted-workers` 返回 0 个已连接 worker，无 SSH/WinRM 配置；`gh api repos/deepfry666/SijiPVToolbox-Releases` 显示 `push=false`，不能创建 Release。Releases 最新仍为 `v1.0.5`，`v1.0.6` 标签不存在。需在 JJ 机器上按 `docs/安装器实施记录_v1.0.6.md` 执行，或在该机器运行 `cursor worker start` 后由代理接管。
  - 标签说明：`Publish-UpdateRelease.ps1` 会在发布记录的 `sourceCommit` 上创建并推送 `v1.0.6`，若标签已指向其他提交会失败，因此不提前打标签；发布提交必须包含重新构建的五个 DLL。具体步骤见 `docs/安装器实施记录_v1.0.6.md`。
  - 当前阻塞：正式构建脚本要求 Windows、已安装且由 Autodesk 签名的 AutoCAD 2020（`acad.exe`、`acmgd.dll` 等）、.NET Framework `csc.exe` 和 Inno Setup；发布脚本需要对 `deepfry666/SijiPVToolbox-Releases` 具有写权限的 `gh`。云端 Linux 代理环境均不具备，不能生成或伪造正式安装器。
  - 验收要求：在维护者 Windows 机器上依次通过 `Build-ReleaseCandidate.ps1 -Version 1.0.6 -RunInstallerRuntimeTest`、AutoCAD 2020 验证、`Publish-TestRelease.ps1`、`New-UpdateRelease.ps1` 和 `Publish-UpdateRelease.ps1`；GitHub latest Release 为 `v1.0.6`，含安装器、`stable.json`、`sha256.txt`、`release.json`，`stable.json` 版本为 `1.0.6` 且 `mirrors` 为空；已安装 `v1.0.5` 能在“检查软件更新”页发现并校验下载 `v1.0.6`；`%APPDATA%\StructuralToolbox` 设置升级后保留。

- [x] `ISSUE-0048` GitHub 最新正式版 v1.0.6 已增加导轨对齐标注，但未包含已在翻译开发分支接入主工具箱的图纸文字翻译模块。
  - 状态：已完成；两功能已整合到 main 并发布为 v1.0.7。
  - 完成日期：2026-10-12。
  - 最终验证：完整回归、六 DLL 构建、CAD 实际命令注册/656 导轨/原尺寸不变、翻译换行替换和保存重开、安装器隔离升级/回滚/卸载均通过；Published 体检 61 项通过。GitHub latest 为公开非预发布 v1.0.7，源码 tag 固定指向 58a1a538d02ab8d11949df00638fa206084b6ef0，四个发布资产下载回验一致；安装器 2261425 字节，SHA-256 为 C442E51EC19017C8E34FD3C2830B6A00BA3137F2E5D11E7240F3FF9B0345BE1E。使用 v1.0.6 更新核心及 .NET Framework 4.7 测试宿主，从实际 latest 地址发现并校验下载 1.0.7 成功。
  - 发布地址：https://github.com/deepfry666/SijiPVToolbox-Releases/releases/tag/v1.0.7；详细证据见 `docs/安装器验证记录_v1.0.7.md`。
  - 来源：用户于 2026-10-12 反馈新版本已发布后发现翻译功能不在其中。
  - 核查证据：发布仓库 `deepfry666/SijiPVToolbox-Releases` 的 latest 为 `v1.0.6`，发布时间为 2026-10-12 14:57:27（北京时间）；`release.json` 指向源码 `a57092975ded06659f24a61b7eda85a1aed54cc6`，下载安装器 SHA-256 与清单一致，为 `3BA7766D55560CDC8A15C113F027471BBBB0A488F948761438A9F5EAC4ADC75D`。该源码的主构建只列五个 DLL，Bundle 没有 `StructuralToolbox.DrawingTranslation.dll` 或翻译命令。
  - 分支关系：`v1.0.6` 与 `codex/update-delivery` 的共同基线为 `8ccc084`；翻译接入提交 `be41241` 及修正提交 `ba5f976` 均不在 `v1.0.6` 历史中。核查时“直接替换/确认后替换”最终调整仍是本地未提交修改；本次已在 `4e098b0` 提交并推送。
  - 验收要求：在保留 v1.0.6 导轨对齐标注的基础上整合翻译模块及最终两种替换模式；独立翻译、主构建、Bundle、安装器和 CAD 验证通过后发布新补丁版，并下载回验安装包与更新清单。

- [x] `ISSUE-0049` 两条开发分支重复使用 ISSUE-0033/ISSUE-0034 表示不同问题，直接合并会破坏问题 ID 的唯一性和历史追踪。
  - 状态：已完成；GDBZ 接入迁移为 ISSUE-0050，v1.0.6 发布迁移为 ISSUE-0051；旧台账中第二个 ISSUE-0031（发布脚本问题）迁移为 ISSUE-0052，原正文和验证证据全部保留。
  - 完成日期：2026-10-12。
  - 验证：所有顶层问题 ID 的唯一性检查通过；相关 v1.0.6 文档引用随迁移更新。
  - 来源：2026-10-12 核查最新发布版与翻译分支时确认。
  - 核查证据：翻译分支的 `ISSUE-0033` 为探索者启动卡死，`ISSUE-0034` 为翻译图形界面验收；v1.0.6 分支的同名 ID 分别表示 GDBZ 接入和 v1.0.6 发布。
  - 验收要求：保留现有问题与完成证据，为重复编号的不同问题分配独立顺序 ID，并更新对应引用；整合后的 ISSUES.md 中每个 ID 只代表同一个问题。

当前执行 ISSUE-0055：撤下导轨标注，恢复翻译完成版本并发布 v1.0.8；`ISSUE-0033` 探索者启动问题保留原状态。其他未完成旧项已按用户要求取消，不再进入开发队列。取消项保留未勾选状态和历史证据，避免被误认为已经修复。

- [x] `ISSUE-0047` 翻译版本守卫脚本仍按接入主工具箱前的独立模块方式编译测试替身，缺少 Contracts 引用，导致守卫验证无法运行。
  - 状态：已完成。
  - 完成日期：2026-10-12。
  - 验证：`work/translation-v107-guards.log` 确认 Contracts 编译、重复加载、磁盘新版本和最终命令清单门禁全部通过。
  - 来源：本次执行 `scripts/verify-drawing-translation-guards.ps1` 时确认。
  - 验收要求：守卫在 `work/` 构建同版本 Contracts，编译和加载当前/更高版本翻译 DLL 时使用同一契约；重复加载、磁盘新版本及最终命令清单门禁全部通过。

- [x] `ISSUE-0046` 图纸文字翻译的最终操作模式应为“直接替换”和“确认后替换”，不再提供“原文下方插入”。
  - 状态：已完成。
  - 完成日期：2026-10-12。
  - 验证：独立核心/API/UI 测试全部通过；守卫确认仅保留最终两种替换模式，CAD 自动换行替换和保存重开通过。日志为 `work/translation-v107-test.log`、`work/translation-v107-guards.log`、`work/translation-v107-cad.log`。
  - 来源：用户于 2026-09-24 明确要求“直接做成两种形式，一种直接替换，一种确认后替换”。
  - 验收要求：主工具箱只显示两个替换入口；直接替换不打开确认窗，确认后替换必须在用户确认后写图；Bundle 不再注册插入命令；两种流程均执行占位符、原文变化和长文字自动换行校验，失败或取消不得修改图纸。

- [x] `ISSUE-0045` 图纸文字翻译接入主工具箱 Contracts 后，两个 AutoCAD 翻译验证脚本未同步引用 Contracts，导致 CAD 测试工具无法编译。
  - 状态：已完成；两个验证脚本均先在 `work/` 构建并引用同版本 Contracts。
  - 来源：本次执行 `scripts/verify-drawing-translation-autocad.ps1` 时确认。
  - 验收要求：离线 CAD 写回验证和真实接口 CAD 验证都从 `work/` 构建并加载同版本 Contracts；验证工具可编译，且不修改根目录原始 DWG。
  - 完成日期：2026-09-24。
  - 验证：`scripts/verify-drawing-translation-autocad.ps1` 编译成功，插入、替换及两次保存重开检查全部通过；原始模板哈希保护通过。

- [x] `ISSUE-0044` 图纸文字翻译的内部占位符 `STPV_TOKEN_*` 会泄漏到预览和最终译文，甚至把占位符本身作为普通文字继续翻译。
  - 状态：已完成；API 结果先严格校验并还原，确认窗只显示最终文字，残留或变形 Token 阻止写入。
  - 来源：用户于 2026-09-24 提供翻译预览截图，译文中出现 `3~_STPV_TOKEN_0000_`，并询问该 Token 来源。
  - 验收要求：内部占位符必须在预览前完整还原；缺失、重复、变形或新增占位符时整条结果拒绝写回并明确提示；图纸和日志不得出现未还原的 `STPV_TOKEN_*`。
  - 完成日期：2026-09-24。
  - 验证：核心测试覆盖缺失、重复、调换、变形及残留 Token；真实 WinForms 消息循环验证 `3~__STPV_TOKEN_0000__` 在预览前还原为 `3~5mm`。

- [x] `ISSUE-0043` “保留原文并在下方插入”按固定偏移放置长译文，未结合原文字框、行高、旋转和相邻内容，导致译文重叠或位置混乱。
  - 状态：已完成；插入文字按原对象实际边界、方向、字高和稳定宽度定位，长译文自动换行。
  - 来源：用户于 2026-09-24 反馈“在下方插入也是会乱”并提供图纸截图。
  - 验收要求：插入文字继承可读的样式和方向，在原文字块局部下方留出明确间距；多行译文按实际高度定位，不与原文自身重叠；不支持安全定位的对象必须跳过并说明原因。
  - 完成日期：2026-09-24。
  - 验证：AutoCAD 2020 Core Console 验证长 DBText 译文位于原文下方、宽度受限且实际高度超过单行，并通过保存重开；表格列译文按列累计实际高度。

- [x] `ISSUE-0042` 中文图纸文字翻译成英文后长度显著增加，插件只替换字符串，没有按原文字区域自适应换行，造成跨区域覆盖和大面积重叠。
  - 状态：已完成；普通单行文字替换为固定宽度 MText，原 MText 沿用或补齐原实际宽度，块属性保持原类型并限制宽度系数。
  - 来源：用户于 2026-09-24 提供翻译后图纸截图并反馈“都太长了没有自适应换行”。
  - 验收要求：多行文字沿用原宽度并产生可控自动换行；单行文字转换为等效多行文字或采用不破坏可读性的受限排版；旋转、对齐、文字高度和图层等关键属性保持一致；替换结果可一次撤销。
  - 完成日期：2026-09-24。
  - 验证：AutoCAD 2020 替换测试确认长 DBText 转换为单个 MText、宽度不小于四倍字高并实际产生多行；实体总数不变，保存重开后排版属性仍有效。

- [x] `ISSUE-0041` 翻译 API 设置窗在代理地址以下发生裁切和重叠，术语表、缓存及“显示服务返回”区域无法完整使用。
  - 状态：已完成；设置内容与底部状态/按钮分区，内容区可滚动，术语和缓存使用稳定高度布局。
  - 来源：用户于 2026-09-24 提供设置窗截图并反馈“代理地址以下的 UI 有显示 bug”。
  - 验收要求：100%、125%、150%、200% DPI 及常用窄屏下，术语、缓存、服务返回、测试连接、保存和取消均不裁切、不重叠，可滚动区域边界清晰。
  - 完成日期：2026-09-24。
  - 验证：设置窗在 100%、125%、150%、200% DPI 以及 `660x590`、`760x650`、`900x700` 客户区布局测试通过；真实 WinForms 测试连接和获取模型交互通过。

- [x] `ISSUE-0040` 翻译设置中的“替换方式”与主工具箱首页的“选择文字并翻译/保留原文并在下方插入”形成双重入口，用户无法判断最终以哪一个为准。
  - 状态：已完成；总设置不再显示写入方式，确认窗不再重复 API 设置或另一种写入入口。
  - 来源：用户于 2026-09-24 询问“替换方式和主页的是不是有冲突”。
  - 补充反馈：用户提供确认窗口截图并要求“总设置里面都有的设置就不要反复出现了”；确认窗口当前又提供“API 设置、自动翻译、在下方插入”，与总设置和主页重复。
  - 验收要求：写入方式只由当前明确执行的首页按钮或命令决定；总设置只保留接口、模型、语言和网络等全局配置；确认窗口不再重复 API 设置或另一种写入方式，只显示本次模式对应的确认按钮、必要进度/取消和最终取消。
  - 完成日期：2026-09-24。
  - 验证：UI 布局与交互测试确认主页分别发出替换/插入命令；确认窗只有“确认替换”或“确认插入”之一，API 设置、旧双写入按钮均不存在。

- [x] `ISSUE-0039` 翻译 API 设置中的模型目前是自由文本输入，获取模型后缺少下拉选择，容易输错或不知道服务端可用模型。
  - 状态：已完成；模型改为不可自由输入的下拉列表，获取成功后直接填充并选择当前或首个模型。
  - 来源：用户于 2026-09-24 要求“模型选取给我下拉的选择方式”。
  - 验收要求：模型控件使用下拉选择；“获取模型”成功后填充并选中当前/推荐模型；列表为空或接口失败时保留可理解的状态反馈，已保存模型可正常回显。
  - 完成日期：2026-09-24。
  - 验证：真实 WinForms 本机模拟服务在模型为空时成功获取 `mock-alpha`、`mock-beta`，下拉项、默认选中、按钮恢复和无重复弹窗均通过。

- [ ] `ISSUE-0033` 安装 `v1.0.5` 后探索者校审系统 2023 无法打开，连续启动产生多个无主窗口的 `acad.exe` 进程。
  - 状态：排查中。
  - 来源：用户于 2026-09-17 安装并尝试启动后反馈“我怎么打不开探索者了”。
  - 当前证据：桌面快捷方式指向 `D:\TSDP2023\TSDPRun.exe`；三次启动均以 `D:\cad\AutoCAD 2020\acad.exe /p TSDP2023 /language zh-CN` 运行，但 `MainWindowHandle=0`。进程停在早期 AutoCAD 模块加载阶段，仅有 2 个线程，持续占用 CPU，尚未加载探索者或 `StructuralToolbox` DLL；Windows 应用程序日志没有对应崩溃记录。
  - 下一步：结束无窗口启动残留，临时禁用并随后恢复 `ZhStructureToolbox.bundle` 做隔离启动；再根据结果区分工具箱 Bundle 扫描冲突、探索者配置或 AutoCAD 宿主问题。
  - 验收要求：探索者快捷方式能稳定打开可见主窗口；不得破坏探索者配置、图纸或工具箱安装；定位结果和恢复步骤有可复核证据。

- [ ] `ISSUE-0034` 图纸文字翻译还缺少图形界面最终验收闭环，用户无法在一次操作中确认当前 DLL、接口配置、翻译结果和撤销状态。
  - 状态：已实现待用户复验；用户首轮主 EXE 测试发现的模型选择、重复入口、设置窗裁切、长文字排版、下方插入和 Token 泄漏已分别由 `ISSUE-0039` 至 `ISSUE-0044` 修复并自动化验证。
  - 来源：用户于 2026-09-24 要求继续完善翻译插件第 1 项。
  - 验收要求：设置、测试连接、获取模型、确认后替换、取消不写入、一次撤销和 DPI 布局均有明确状态反馈与自动化覆盖。
  - 当前待办：完全退出 AutoCAD 后安装本轮主 EXE，打开同一项目图纸，从 `JJJJ` 首页复验模型下拉、设置窗、确认替换、下方插入、一次撤销和实际视觉排版。

- [x] `ISSUE-0038` 图纸文字翻译当前仍需单独 NETLOAD，未进入主工具箱 Bundle 和主安装器，现场测试入口不够方便。
  - 状态：已完成；翻译模块已接入主工具箱首页、六模块构建、Bundle 清单和安装器负载校验。
  - 来源：用户于 2026-09-24 要求“直接做到主exe里面，方便测试”。
  - 验收要求：主工具箱首页能打开翻译模块；主构建、Bundle 清单、安装器负载和模块隔离测试通过；翻译命令仍保持独立版本和原有安全边界。
  - 完成日期：2026-09-24。
  - 验证：`scripts/build.ps1 -PassThru` 成功生成六个模块并写入 `bundle/ZhStructureToolbox.bundle`；完整 `scripts/test.ps1`、独立 `scripts/test-drawing-translation.ps1`、主工具箱 UI、安装器制品和隔离安装/升级/回滚/卸载门禁通过；AutoCAD 2020 Core Console 返回 `MODULES=6` 并验证翻译命令注册。

- [x] `ISSUE-0035` 翻译失败、取消或部分对象失效时，当前反馈不够细，用户无法快速知道哪些对象被跳过以及图纸是否发生修改。
  - 状态：已完成；预检摘要、取消按钮、失败提示和写入/跳过日志已补齐。
  - 来源：用户于 2026-09-24 要求继续完善翻译插件第 2 项。
  - 验收要求：接口或校验失败不得写图纸；取消和原文变化只影响对应对象；命令行、弹窗和日志报告写入、跳过、失败数量。
  - 完成日期：2026-09-24。
  - 验证：核心/API/UI 门禁全绿；真实接口替换 8/8、插入 8/8、一次 UNDO 还原 8/8，模板图 SHA-256 未变化。

- [x] `ISSUE-0036` 翻译插件缺少术语表、译文缓存和可见的批量进度/取消/重试设置，重复处理大图纸时效率和可控性不足。
  - 状态：已完成；设置面板支持术语表、缓存有效期和清空缓存，确认窗显示批量进度并可取消，API 层保留分批和失败重试。
  - 来源：用户于 2026-09-24 要求继续完善翻译插件第 3 项。
  - 验收要求：术语和缓存不写入图纸或明文密钥；缓存命中不请求接口；大批量可取消、失败可重试且条目顺序不变。
  - 完成日期：2026-09-24。
  - 验证：API 设置和缓存 round-trip、术语表请求体、进度回调、40 条分批和截断拆分测试通过；API/UI 门禁全绿。

- [x] `ISSUE-0037` 翻译选择范围对块属性等文字对象支持不足，且不支持对象的跳过原因没有按类型汇总。
  - 状态：已完成；增加非外部参照、非常量块属性的安全读取/写回，并按对象类型汇总跳过原因。
  - 来源：用户于 2026-09-24 要求继续完善翻译插件第 4 项。
  - 验收要求：块属性仅在非外部参照且句柄/原文未变化时可写；不支持对象不得静默丢失；核心和 AutoCAD 回归通过。
  - 完成日期：2026-09-24。
  - 验证：模块构建通过；写入前继续校验块属性的外部参照、常量、句柄和原文状态；不支持对象在命令行输出分类摘要。

- [x] `ISSUE-0032` 组件多段线视觉上首尾闭合，但 AutoCAD `Closed` 属性为否时会被组件识别直接拒绝。
  - 状态：已完成；组件识别改为同时判断 `Closed` 属性和首尾端点几何距离。
  - 来源：用户于 2026-09-17 反馈“组件看着是多段线首尾闭合，但是属性里是未闭合，如果属性里未闭合就不允许作为组件”。
  - 当前根因：`ArrayComponentReader` 对轻量 `Polyline` 和旧式 `Polyline2d` 都先检查 `Closed` 属性；即使末点与起点重合或仅有微小制图误差，也会在读取顶点前返回“未闭合”。
  - 定版方向：保留 `Closed=true` 的现有行为；当 `Closed=false` 时读取首尾点并按明确的毫米容差判断几何闭合，容差内接受并去掉重复末点，超出容差继续拒绝且报告实际缺口；只改变识别结果，不修改原图实体属性。
  - 实施进展（2026-09-17）：轻量和旧式多段线已统一使用几何闭合判定，闭合容差为 `0.1 mm`；首尾重合点会在四边形判断前规范化。15 项阵列识别核心测试、配重模块专项测试、两个实际模块构建及完整 `scripts/test.ps1` 回归通过。`v1.0.5` 候选、安装器隔离运行、AutoCAD 2020 五模块加载、正式输出及 GitHub 下载回验均已通过。
  - 完成日期：2026-09-17。
  - 最终验证：`v1.0.5` 已发布为 GitHub latest Release，非草稿、非预发布；线上 `stable.json` 为 `1.0.5`，安装包地址直接指向 GitHub 版本资产且 `mirrors` 为空；安装器、`stable.json`、`sha256.txt`、`release.json` 四个资产全部下载回验，远端安装器为 `2210227` 字节，SHA-256 为 `2C6EFABE54021C6376D8712F3930F7C4E762340D738CB5062C687CF248464A4A`。Release：`https://github.com/deepfry666/SijiPVToolbox-Releases/releases/tag/v1.0.5`，源码 tag `v1.0.5` 指向 `2b8f4a3185d4de62424faf7861af4f0ec81e8c6e`。
  - 验收要求：轻量和旧式多段线共用同一闭合规则；首尾完全重合及容差内近闭合可识别为组件，明显开口仍被拒绝并报告缺口；独立阵列识别和正式配重模块回归通过；发布补丁版本并完成 GitHub Release 下载回验。

- [x] `ISSUE-0021` 完整回归脚本在 Windows PowerShell 5 下因无 BOM 的 UTF-8 中文路径解析失败，导致导轨标注源码检查无法定位 `导轨对齐标注.lsp`。
  - 状态：已完成；脚本编码已统一为 Windows PowerShell 5 可识别的 UTF-8 BOM。
  - 来源：项目审查于 2026-09-14 执行 `scripts/test.ps1` 时发现；前序测试项通过，`tests/RailAnnotationSourceTests.ps1` 在读取源码阶段失败。
  - 复现：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`；错误路径中的中文文件名被解析为乱码，实际文件为 `src/StructuralToolbox.RailAnnotation/导轨对齐标注.lsp`。
  - 初步根因：测试脚本及相关 PowerShell 文件为无 BOM UTF-8，Windows PowerShell 5 按系统 ANSI 代码页解释脚本中的中文字面量；脚本同时依赖中文文件名，编码错误直接变成 FileNotFoundException。
  - 建议修复：统一脚本编码策略（为需兼容 Windows PowerShell 5 的脚本使用 UTF-8 BOM，或改用 ASCII/编码无关的文件定位），并在干净 PowerShell 5 环境重新跑完整回归。
  - 完成日期：2026-09-14。
  - 验证：Windows PowerShell 5 执行 `scripts/test.ps1` 通过；导轨标注源码、GBK 构建和编码往返检查均执行到 PASS；未重命名或删除中文交付文件。
  - 验收要求：完整 `scripts/test.ps1` 通过；导轨标注源码、构建和编码往返检查均执行到 PASS；不得通过重命名或删除中文交付文件规避检查。

- [x] `ISSUE-0028` Windows PowerShell 5 调用带有 `$PSScriptRoot` 参数默认表达式的子脚本时，参数绑定阶段可能得到空路径，导致发布工作流在 `Split-Path` 处失败。
  - 状态：已完成；相关脚本改为先接收空参数，再在脚本体内根据 `$PSScriptRoot` 初始化默认路径。
  - 来源：修复 `ISSUE-0021` 后执行 Windows PowerShell 5 完整回归时复现。
  - 完成日期：2026-09-14。
  - 验证：`scripts/test.ps1` 全部通过，其中包含 60 项发布工作流、12 项工作目录清理和更新服务回归测试。

- [ ] `ISSUE-0022` 项目没有单一的构建真相源，旧式 csproj 的 `Compile Include`、多个手工 csc PowerShell 构建脚本和发布脚本分别维护源码清单，容易出现漏编译或构建结果不一致。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 证据：`src/*/*.csproj` 与 `scripts/build-*.ps1` 同时维护文件列表；仓库没有 `.sln`、`Directory.Build.props` 或统一项目编排入口。
  - 建议方向：先建立统一解决方案/构建清单，由一个入口生成模块构建和测试输入；保留 AutoCAD 2020 专用编译器校验，但删除重复的源码列表。

- [ ] `ISSUE-0023` `StructuralToolbox.Ballast` 通过链接编译方式复制 `StructuralToolbox.ArrayRecognition` 的核心和读取器，独立阵列识别项目与正式配重模块可能发生版本漂移。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 证据：`src/StructuralToolbox.Ballast/StructuralToolbox.Ballast.csproj` 的 `Compile Include="..\\StructuralToolbox.ArrayRecognition\\..."` 条目；同一能力另有独立 DLL 构建脚本。
  - 建议方向：把纯识别核心提取为稳定库并使用 `ProjectReference`/版本契约；AutoCAD 命令适配层单独保留，避免模块复制源码。

- [ ] `ISSUE-0024` 主壳按目录通配加载所有 `StructuralToolbox.*.dll`，且模块契约直接暴露 WinForms 控件和首选 Palette 尺寸，开发、独立交付和实验 DLL 放入同目录时存在误加载和宿主布局耦合风险。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 证据：`src/StructuralToolbox/UI/ToolboxModuleLoader.cs` 使用 `Directory.GetFiles(directory, "StructuralToolbox.*.dll")`；`src/StructuralToolbox.Contracts/IToolboxModule.cs` 同时定义 `Control CreateControl()` 与 `PreferredPaletteSize`。
  - 建议方向：使用 Bundle/模块清单 allowlist、契约版本和加载失败隔离；把宿主 UI 尺寸策略从模块接口移出。

- [ ] `ISSUE-0025` Rail、Layout、Ballast、PvComponent 和 ArrayRecognition 各自维护几何类型与数值容差，跨模块结果可能出现边界判断不一致。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 证据：各模块均有独立 `Core/*Geometry.cs` 和本地 tolerance 常量；当前没有共享几何/容差基础库。
  - 建议方向：先抽取只含二维向量、边界、相交和容差策略的 `StructuralToolbox.Geometry`，再逐模块迁移并用现有回归锁定行为。

- [ ] `ISSUE-0026` 关键命令和服务类过大且夹杂交互、事务、几何和统计职责，全局会话状态增加跨图纸和重复运行的维护风险。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 证据：`RailLayoutEngine.cs` 约 1340 行、`BallastCommands.cs` 约 899 行、`RailDrawingService.cs` 约 874 行；`ArrayRecognitionCommands` 使用进程级静态 `_session` 保存识别结果。
  - 建议方向：按“输入读取→纯求解→结果模型→绘图事务→统计输出”拆分，采用按 Document 隔离的会话仓储，先从高变更命令开始。

- [ ] `ISSUE-0027` 当前文档和脚本同时保留大量版本历史、实验模块和正式发布流程，README、待修改清单与 ISSUES 台账的状态边界不够清晰，新成员难以判断当前唯一入口和有效范围。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 证据：`docs/` 现有 67 个文件、`scripts/` 现有 47 个脚本；活动目录与历史版本说明并列，且仍有多个未完成问题处于“等待用户验收”和“尚未实现”混合状态。
  - 完成本阶段：2026-09-14；验证：新增当前开发入口、脚本分组说明，README 已链接到当前入口；完整 `scripts/test.ps1` 通过。
  - 后续建议：建立历史归档索引并逐步清理 README 中的版本重复内容；把问题台账状态拆成“未修复、已修复待验收、已完成”。

- [x] `ISSUE-0020` 在线更新页面向最终用户展示了内置更新源地址和锁定提示，新版本弹窗也会显示清单来源地址。
  - 状态：已完成；更新页和结果弹窗均不再渲染更新源，固定 HTTPS 地址仅在程序集内部使用。
  - 来源：用户于 2026-09-11 根据 AutoCAD 实机截图提出“不要展示这个更新源”。
  - 完成日期：2026-09-11
  - 范围：移除更新页的“更新源”标签、地址输入框和“更新地址已锁定”提示；新版本弹窗不再显示清单来源地址。固定 HTTPS 清单仍保留在程序集内部，检查、下载、大小及 SHA-256 校验逻辑不变。
  - 验证：8 项更新 UI 源码测试、24 项工具箱 UI 布局、完整 `scripts/test.ps1`、`v1.0.2` 候选静态制品与隔离安装/升级/回滚/卸载、AutoCAD 2020 Core Console 五模块加载及 Published 体检通过；正式安装器 SHA-256 为 `70ECC5B5C77B7212F1A8D210C112BC56268A7D92CB9F8DD57FC1EF6D4D44F7F5`。用户安装并重启 AutoCAD 后仍需做最终外观确认，但实现、自动化及发布验证均已完成。
  - 验收要求：更新页和结果弹窗均不渲染更新源 URL；检查按钮与状态区在约 `340 px` 宽及 100%、125%、150%、200% DPI 下无裁切或重叠；在线清单检查、版本比较和安装包校验回归通过；新构建安装并重启 AutoCAD 后实机确认。

- [ ] `ISSUE-0019` 生产在线更新域名的 HTTPS 证书和 443 路由尚未满足客户端锁定地址，且公开稳定版清单仍停留在旧版。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户于 2026-09-11 表示服务器和域名已准备完成，并询问软件更新方式；随后对正式更新地址进行在线核查。
  - 当前客户端地址：`https://download.imfourj.cn/fourj-pv-toolbox/stable.json`，从 `v0.4.1` 起已锁定在更新模块程序集内。
  - 当前在线证据（2026-09-11）：
    1. `download.imfourj.cn` 的 A 记录已解析到 `139.196.199.45`。
    2. 443 端口返回的证书主题为 `CN=cpa.imfourj.cn`，SAN 仅包含 `cpa.imfourj.cn` 和 `cpam.imfourj.cn`，与更新域名不匹配；正常 HTTPS 客户端因此拒绝连接。
    3. 忽略证书校验访问正式 HTTPS 清单路径返回 `404`；HTTP 同一路径可读取清单，但客户端明确拒绝 HTTP 更新源。
    4. HTTP `stable.json` 仍声明 `v0.4.0`、`2186400` 字节和 SHA-256 `8e9dd50e9c577ec1ba229ddcd7be5431375fc64976e537197673e77dd6bf27e9`；当前正式交付为 `v1.0.1`，安装器为 `2198282` 字节，SHA-256 `b1bd1f5e5d35beaf471cfc53a7135ef5f47f24e7b1d866fb3503afc7525d26b0`。
  - 部署进展（2026-09-11）：已为 `download.imfourj.cn` 单独签发并由 Certbot 部署 Let's Encrypt 证书，证书有效期至 2026-12-10，自动续期任务已建立；不改动 `cpa.imfourj.cn`、`game.imfourj.cn` 等其他虚拟主机。
  - 外网验证（2026-09-11）：HTTPS `stable.json` 返回 `200 application/json` 和 `Cache-Control: no-store, max-age=0`，版本为 `1.0.1`；HTTP 清单地址返回 `301` 到同域 HTTPS；下载页返回 `200`；版本化 EXE 返回 `200 application/octet-stream` 和 immutable 长缓存。外网实际下载文件为 `2198282` 字节，SHA-256 为 `b1bd1f5e5d35beaf471cfc53a7135ef5f47f24e7b1d866fb3503afc7525d26b0`，与清单完全一致。
  - 安全边界：必须继续使用完整 EXE、HTTPS、大小和 SHA-256 校验；安装包应先上传，`stable.json` 最后原子切换；保留上一稳定版用于回滚，不启用 DLL 热更新或绘图过程中静默安装。
  - 验收要求：
    1. 为 `download.imfourj.cn` 部署名称匹配且有效的证书，并让对应 443 `server_name` 提供 `/fourj-pv-toolbox/` 静态站点；HTTP 仅重定向到同域 HTTPS，不得降级。
    2. 使用仓库生成器发布 `v1.0.1` 站点，确保 `stable.json`、版本化 EXE、文件大小和 SHA-256 一致，且线上清单禁用缓存、版本化安装包可长期缓存。
    3. 本地 `Test-UpdateSite.ps1` 和线上 HTTPS 请求均通过；不跳过证书验证时清单、下载页和安装包返回正确状态与内容类型。
    4. 在 AutoCAD 2020 中验证 `v0.4.1` 或更高旧版能发现、下载并校验 `v1.0.1`，`v1.0.1` 显示已是最新；`v0.4.0` 因不含更新模块，需人工安装一次新版。

- [ ] `ISSUE-0018` 增加图纸文字翻译功能，支持自动翻译后直接替换、确认后替换，以及在原文下方另行插入。
  - 状态：功能已实现并通过真实 API 实机验证（2026-09-23）；直接替换、下方插入、接口自检和一次 `UNDO` 已由 AutoCAD 2020 实机脚本验证，UI 布局测试覆盖 100%/125%/150%/200% DPI。仍需用户在图界面确认设置面板保存/测试连接/获取模型与“确认后替换”对照窗；当前交付 preview9（0.2.5.0），执行前会拦住磁盘上的陈旧版本，用户需完全退出 AutoCAD 后重新 NETLOAD 复测，确认前本项保持未勾选。
  - 实机问题（2026-09-23）：用户在直接替换流程看到“翻译写入失败，图纸未修改：无效的 JSON 基元”；当前响应解析强制把 `choices[0].message.content` 当作 JSON 数组，单条纯文本译文会触发原始解析异常。修复需兼容单条常见格式，批量结果继续严格校验数量，任何解析失败均不写图纸；保留实机复测待办。
  - 修复验证（2026-09-23）：解析器现在接受单条纯文本、JSON 字符串、JSON 数组、带代码围栏的数组和包含 `translation(s)` 的对象；批量仍要求 JSON 及条目数一致，异常格式统一给出可读错误。模拟 API 回归通过，AutoCAD 实际配置尚待用户用新版 DLL 复测，本项保持未完成。
  - 复测纠正（2026-09-23）：用户加载修正版后仍收到“翻译服务响应不是有效 JSON”。安全读取配置确认 Endpoint 为 `https://cdn.coderelay.cn/` 站点根地址；GET 根地址返回 200 `text/html`，无密钥 POST `/v1/chat/completions` 返回 401 `application/json`。前次把根地址返回网页误判为模型译文格式问题。需自动规范化基础地址、拒绝非 JSON 响应并用真实已配置接口验证；本项仍未完成。
  - 真实接口诊断（2026-09-23）：根地址补全为 `/v1/chat/completions` 后，.NET Framework 默认 TLS 协商失败；启用 TLS 1.2 后接口返回 HTTP 503。当前密钥的 `/v1/models` 查询返回 200，但仅列出 `gpt-image-2`、`gpt-image-2-2k`，不包含所填 `gpt-5.6-terra`，没有已列出的文本模型。需保留用户设置、给出模型权限提示；当前配置无法完成文字翻译，等待用户提供可用文本模型权限或更换 API 服务。
  - 根因确认与修复（2026-09-23）：实机“不是有效 JSON”由两处可复现原因造成：已保存接口地址是站点根地址 `https://cdn.coderelay.cn/`，旧 DLL 直接 POST 根地址得到 200 `text/html`；本机系统代理 `127.0.0.1:7897` 转发 HTTPS 时 TLS 握手失败。现已自动补全 `/v1/chat/completions`，收到网页时直接说明是非 JSON 内容，并新增“网络连接”（直连 / 系统代理 / 自定义代理，默认直连）与失败自动切换重试。
  - 真实接口复测（2026-09-23）：直连与强制系统代理两种配置都给出可执行提示；`https://cdn.coderelay.cn/v1/models` 和 `https://coderelay.cn/v1/models` 只返回 `gpt-image-2`、`gpt-image-2-2k`，确认当前密钥分组没有文本模型，代码侧无法完成文字翻译。新增“获取模型”按钮可直接读取并显示密钥可用模型；6 项核心测试、API 测试、模型列表测试、UI 布局测试与独立 DLL 构建全部通过，交付 `outputs/StructuralToolbox.DrawingTranslation_preview3_AutoCAD2020`。本项保持未完成，阻塞点为需要具备文本模型权限的密钥或服务。
  - 补充验证（2026-09-23）：本机 `127.0.0.1` 回环 HTTP 服务端到端请求通过（地址补全、Bearer 头、请求体、译文解析全部正确）；AutoCAD 2020 Core Console 用 `模板图.dwg` 副本再次通过插入、重开、替换、替换重开四项验证，原始模板 SHA256 未变化。
  - 本轮需求（2026-09-23）：用户实机截图显示译文列只是带保护标记的原文，点击插入提示“没有需要应用的译文”；随后要求增加自行填写的 API 接入面板。需支持配置、自动翻译到可审阅的译文列、再确认插入，失败时不写图纸。
  - 新增选择（2026-09-23）：用户要求译文可直接替换原文字，也可在原文下方另行插入；两种模式均需实测，替换应保持对象位置和类型。
  - 最终流程纠正（2026-09-23）：用户要保存的两种替换流程是“直接替换（无逐条确认）”和“确认后替换”，避免每次都确认；原有下方插入作为独立命令保留。
  - 本轮进展（2026-09-23）：独立模块新增 OpenAI 兼容 API 设置（完整端点、模型、密钥、目标语言、替换流程），密钥用当前 Windows 用户 DPAPI 保护；`STPVTRANSLATE` 按已保存流程自动识别来源语言并翻译，直接模式无确认窗，确认模式自动填入译文供审阅；`STPVTRANSLATEINSERT` 保留下方插入。6 项核心测试、API 模拟请求/密钥存储测试、窗口布局测试通过；AutoCAD 2020 Core Console 图纸副本验证原文替换不增加实体、插入保留原文以及两种结果保存重开均通过。尚需用户提供真实 API 配置后实测请求、AutoCAD 图形界面直接/确认流程、撤销和 DPI；本项保持未完成。
  - 需求纠正（2026-09-23）：前一阶段独立模块误做成译文预览及原对象回写，不能作为本需求交付。该原型曾通过 6 项核心测试、DLL 构建和 2 种窗口尺寸布局测试，现已撤销其回写和 Xrecord 备份逻辑。
  - 修正进展（2026-09-23）：`STPVTRANSLATE` 保留选择集和译文输入，仅新增独立 MText；DBText、MText 译文在原文下方，表格单元格译文按列排列于表格下方。原对象只读，插入前重新核对句柄、类型与原文。6 项核心测试、独立 DLL 构建、2 种窗口尺寸布局测试及 AutoCAD 2020 Core Console 图纸副本插入/保存重开验证通过；四条译文均新增且原文未变，同列表格译文依次排列。仍需图形界面验证实际选择、单次撤销和 100%/125%/150%/200% DPI；API 自动翻译尚未接入，问题保持未完成。
  - 来源：用户于 2026-09-09 提出“加一个图纸文字翻译功能”。
  - 当前任务：独立验证自动翻译及两种替换流程；原有下方插入能力单独保留，真实 API 配置由用户填写。
  - 首版边界：独立翻译模块；优先支持模型空间顶层 `DBText`、`MText` 和表格单元格；替换模式写回同一实体，插入模式新增独立文字；一次 `UNDO` 的界面验证待完成；不递归处理块参照/Xref，不翻译尺寸数值、图层名和代码标识符。
  - 待确定事项：具体服务和目标语言由用户在 OpenAI 兼容接口面板填写；术语表/项目词典、是否需要批量整张图和块内文字支持仍待确定。
  - 实施前审计结论（2026-09-09）：
    1. `IToolboxModule` 及 `ToolboxModuleLoader` 支持独立 DLL 自动发现，但正式构建、Bundle、安装器和发布追溯仍有固定模块名单，接入时必须同步更新，不能只新增 csproj。
    2. AutoCAD 2020 API 可直接读取 `DBText.TextString`、`MText.Contents/ContentsRTF`、`AttributeReference.TextString/MTextAttribute`、`Table.TextString(row,column)`；`MText.Text` 不适合作为保格式回写源。
    3. `MLeader.MText`、`Dimension.DimensionText`、字段和块参照存在关联内容或动态值，首版必须分类报告并默认跳过高风险对象；尺寸真实测量占位符 `<>` 不得送入翻译服务。
    4. 顶层实体扫描与块/Xref 递归必须分开建模；Xref 不应在宿主图中直接改写，块参照还需处理 BlockTransform、属性和嵌套循环保护。
    5. 选择阶段不得打开写事务；插入阶段按对象快照校验句柄、类型和原文未变化后再新增实体，失败项不得改变原文。一次 `UNDO` 的完整撤销需在 AutoCAD 2020 图形界面验证，当前仓库没有现成的显式 Undo 标记封装。
    6. 首版自动化测试应覆盖格式码/换行/字段/数字单位保护、表格单元格、重复对象去重、取消、翻译失败、对象被外部修改和保存重开；UI 需覆盖 100%、125%、150%、200% DPI 及紧凑宽度。
  - 真实链路验证通过（2026-09-23）：根因是旧 DLL 只读顶层 `choices`，而中转把结果包在 `data.choices` 里。现已兼容 `choices`、`data.choices`、`data[]` 三种位置与 `content` 字符串/分片数组两种写法，并新增 `STPVTRANSLATEINFO` 打印 DLL 版本、路径、修改时间和生效接口配置。
  - 实机证据（2026-09-23）：AutoCAD 2020 Core Console 用 `模板图.dwg` 副本、真实 API（`https://cpa.imfourj.cn/v1`，模型 `deepseek-v4.1-flash`）验证——直接替换 `STPV_TRANSLATION_LIVE_PASS targets=8 written=8 skipped=0`，8 条全部变英文、中文残留 0、位置未变；下方插入 `STPV_TRANSLATION_LIVE_INSERT_PASS targets=8 written=8`，原文 8 条未变、新增 8 条 MText 译文逐条位于对应原文下方（例：`北` y=169863.107 → `North` y=167692.894）。QSAVE 后另起进程重开回读一致，原始模板 SHA256 未变化。复现脚本 `scripts/verify-drawing-translation-live.ps1`（`-Mode replace|insert|both`）。
  - 交付与待办（2026-09-23）：交付 `outputs/StructuralToolbox.DrawingTranslation_preview4_AutoCAD2020`（程序集 0.2.0.0）。图形界面里 `STPVTRANSLATESETTINGS` 的保存/测试连接/获取模型、`STPVTRANSLATE` 的“确认后替换”对照窗、`STPVTRANSLATEINSERT`、一次 `UNDO` 以及 100%/125%/150%/200% DPI 仍需用户实测确认。
  - 凭据说明（2026-09-23）：为打通真实链路，验证用的中转地址与令牌已写入本机翻译配置（`%LOCALAPPDATA%\FourSeasonsPVToolbox\DrawingTranslationApi.json`，DPAPI 保护）。该令牌来自本机 Codex 配置，用户可在面板中替换或清空，已向用户披露。
  - 复测失败（2026-09-23）：用户用 preview4（程序集 0.2.0.0）在直接替换流程再次看到“翻译或写入失败，图纸未修改：翻译服务响应不是有效 JSON，图纸未修改。”。逐项复现确认该中转响应形态不稳定：同一地址同一密钥分别返回顶层 `choices`、`{"data":{"choices":[...]}}`，还会把整个响应包进 JSON 数组 `[{...}]`；按 60 条请求正常，按 150 条请求时响应被截断成半截 JSON。旧代码只按字典解析顶层、且一次性提交全部文字，遇到顶层数组或截断就报“不是有效 JSON”。
  - 根因修复（2026-09-23）：`TranslationApi` 改为每批最多 40 条分批请求；响应解析改为逐级宽松尝试（顶层字典、`data` 包裹、顶层数组、SSE 分片、JSON 前后夹带提示文本）；空响应和半截 JSON 先原样重试一次，仍失败则拆成两半分别请求，而数量或结构不符的语义错误直接失败、不再用拆批掩盖；错误信息改为带 HTTP 状态、Content-Type、响应长度、响应开头和服务端说明，并用同一密钥读一次模型列表区分“密钥或模型不对”和“网关响应异常”。
  - 修复验证（2026-09-23）：真实接口大批量复测 8/45/150 条分别完整返回 8/45/150 条非空译文（`changed` 等于请求条数、空译文 0），150 条连跑两遍都通过，旧版会被截断；请求按 40 条一批拆分。AutoCAD 2020 实机 `-Mode both` 全通过：替换 8/8、插入 8/8、接口自检 `models=8`、一次 `UNDO` 还原 8/8 并重开图纸回读一致，原始模板 SHA-256 未变化。`scripts\test-drawing-translation.ps1` 全绿。
  - 交付（2026-09-23）：`outputs/StructuralToolbox.DrawingTranslation_preview5_AutoCAD2020`（程序集 0.2.1.0，73216 字节，SHA-256 `6FFCC2CF8F8B759B7189092FBF8D02CCF3FCCAF518D92F0F3A85559DDA065D25`）。用户需重启 AutoCAD 后重新 `NETLOAD` 并用 `STPVTRANSLATEINFO` 确认版本为 0.2.1.0。
  - 复测再失败（2026-09-23）：用户在图形界面再次看到“翻译或写入失败，图纸未修改：翻译服务响应不是有效 JSON，图纸未修改。”。逐字节核对各版本 DLL 后确认这句文案只存在于 preview、preview2、preview3、preview4；preview5（0.2.1.0）已经换成“响应内容不是有效 JSON…”，也就是说当时 AutoCAD 执行的仍是旧程序集：同一个 AutoCAD 会话不能卸载已 NETLOAD 的 DLL，再次 NETLOAD 新文件不会替换会话里已经注册的旧命令。图纸未被修改。
  - 自检改进（2026-09-23）：`STPVTRANSLATEINFO` 现在列出“模块加载：已加载 N 份”以及每一份的版本和路径（加载多份时直接提示必须完全退出 AutoCAD 再 NETLOAD）；新增诊断日志 `%LOCALAPPDATA%\FourSeasonsPVToolbox\DrawingTranslation.log`，记录命令、模块版本与路径、接口配置、选择数量、翻译条数、写入结果和失败详情；新增 `STPVTRANSLATELOG` 直接打开该日志。
  - 自检验证（2026-09-23）：新增 14 项诊断测试（日志路径、日志写入、同名程序集枚举、多份加载提示）全部通过；`scripts\test-drawing-translation.ps1` 全绿；AutoCAD 2020 Core Console 用交付 DLL 实机 `NETLOAD` + `STPVTRANSLATEINFO` 输出 0.2.2.0、已加载 1 份、路径为 preview6，原始模板 SHA-256 未变化；真实接口 `-Mode both` 回归替换 8/8、插入 8/8、接口自检通过、一次 UNDO 还原 8/8 且重开一致。
  - 交付（2026-09-23）：`outputs/StructuralToolbox.DrawingTranslation_preview6_AutoCAD2020`（程序集 0.2.2.0，76800 字节，SHA-256 `109D14F5C60601931B17361232720D20A07CC790E67B5BBD6517C006A944DAC0`）。用户操作：完全退出 AutoCAD（含所有已打开窗口）→ 重新打开 → `NETLOAD` 该 DLL → `STPVTRANSLATEINFO` 确认“版本 0.2.2.0”“已加载 1 份”→ 再试翻译；若仍失败，把 `DrawingTranslation.log` 发回。
  - 根因更正（2026-09-23）：用户复测截图里的报错文案仍是“翻译服务响应不是有效 JSON，图纸未修改。”。再次逐字节核对交付过的每一份 DLL：该文案只存在于 preview、preview2、preview3、preview4；preview5/6 已是“响应内容不是有效 JSON…”。本机 `NETLOAD` 最近记录指向 `outputs/StructuralToolbox.DrawingTranslation_preview2_AutoCAD2020`，说明当时执行的是 preview2 旧程序集，不是新版解析失败，问题是“换版本必须完全退出 AutoCAD”这一步没有生效。
  - 版本自证改进（2026-09-23）：程序集升到 0.2.3.0；成功提示、失败弹窗标题和审阅窗口标题统一附带“模块 版本 · DLL 文件名”；模块实现 `IExtensionApplication`，`NETLOAD` 与卸载都写日志；同一会话加载多份模块时，失败弹窗直接提示必须完全退出 AutoCAD 再 `NETLOAD`。
  - 界面自动化证据（2026-09-23）：新增 6 个界面交互场景（测试连接成功/失败、获取模型、确认窗自动翻译并确认写入、取消不写入、翻译失败不写入），由 `scripts\test-drawing-translation.ps1` 一并编译运行；连同核心 6 项、诊断 14 项、API 测试与 UI 布局测试全部通过。
  - 交付与自检（2026-09-23）：`outputs/StructuralToolbox.DrawingTranslation_preview7_AutoCAD2020`（程序集 0.2.3.0，77824 字节，SHA-256 `A7D24483B544C20AFF60CC221427B8D38141F0B0857D9DDBCA70821901EA40F9`）。AutoCAD 2020 Core Console 用该交付 DLL `NETLOAD` + `STPVTRANSLATEINFO` 自检输出 0.2.3.0、已加载 1 份、路径为 preview7，诊断日志同时记录 NETLOAD 与卸载；真实接口 `-Mode both` 回归替换 8/8、插入 8/8、接口自检 `models=8`、一次 `UNDO` 还原 8/8 且重开一致，原始模板 SHA-256 未变化。本项保持未勾选，等用户在图形界面确认版本指纹和两种替换流程。
  - 执行前版本守卫（2026-09-23）：新增交付目录版本扫描，`STPVTRANSLATE`、`STPVTRANSLATEINSERT` 每次运行前先比较正在执行的程序集与 `outputs\StructuralToolbox.DrawingTranslation_*` 同级交付版本；只要磁盘上存在更高版本，命中即中止（不选对象、不改图纸）并弹窗列出新版本路径，要求完全退出 AutoCAD 后重新 `NETLOAD`。程序集升到 0.2.4.0。为会话内确实加载了第二份模块的情形保留了“本次加载不会生效”提示；实测该提示多数情况下不会出现，因为宿主会直接忽略同名程序集的第二次加载（见旧会话实测）。
  - 重复 NETLOAD 实测（2026-09-23）：新增探针 DLL 交叉验证，同一 AutoCAD 会话内第二次 `NETLOAD` 同名程序集被宿主静默忽略——程序集名相同、版本分别为 1.0.0.0 与 2.0.0.0 的两份探针先后加载后，命令始终由最先加载的 1.0.0.0 执行（`PROBE_BANNER=X1`），另一组 Y1/Y2 结果相同；同一操作在 accoreconsole 中命令本身可正常执行，说明是宿主行为而非环境故障。换版本必须完全退出 AutoCAD。
  - 旧会话实测（2026-09-23）：AutoCAD 2020 Core Console 先 `NETLOAD` preview2（0.1.0.0）再 `NETLOAD` preview8（0.2.4.0），第二次加载没有任何提示、新版模块初始化未执行，随后 `STPVTRANSLATEINFO` 报「未知命令“STPVTRANSLATEINFO”」，命令仍由旧版处理。也就是说，旧版已加载时再 NETLOAD 新 DLL 会被宿主静默忽略，不会看到任何警告；「`STPVTRANSLATEINFO` 是否报未知命令」是判断当前会话跑的是哪一版的最快方法，换版本必须完全退出 AutoCAD。该场景已写入 `scripts\verify-drawing-translation-guards.ps1` 固化，随机场景一起运行。
  - 守卫验证（2026-09-23）：`scripts\test-drawing-translation.ps1` 全绿（核心 6 项、诊断 36 项、API 测试、模块构建、UI 布局测试、UI 交互 6 场景）；新增可重复执行的 `scripts\verify-drawing-translation-guards.ps1`，在 AutoCAD 2020 Core Console 上 21 项检查全 PASS（单独加载记录 NETLOAD 版本、只有一份模块、不提示重复；磁盘无更新版本不打扰；同会话二次 NETLOAD 后仍执行 0.2.4.0 且只留下 1 条记录；加载陈旧交付副本时 NETLOAD 当场提示磁盘更新版本；`STPVTRANSLATEINFO` 报 0.2.4.0 并同时报出磁盘更新版本；交付 DLL 含拦截文案）。
  - 交付（2026-09-23）：`outputs/StructuralToolbox.DrawingTranslation_preview8_AutoCAD2020`（程序集 0.2.4.0，82432 字节，SHA-256 `6071F1C4FFD9AB43FA4758417B9400399E6AAB5725302855FA4C2522B3DF8D26`），README 与 SHA256SUMS 同步交付。真实接口 `-Mode both` 回归替换 8/8、插入 8/8、接口自检 `models=8 first=gpt-5.6-sol`、一次 `UNDO` 还原 8/8 且重开一致，原始模板 SHA-256 未变化。
  - 验证边界（2026-09-23）：accoreconsole 中走 `RunTranslation` 的图形执行路径（对象选择、WinForms 弹窗）会静默终止，完整界面流程无法脚本化验证，只能靠单元测试、交付 DLL 二进制核对与用户在 AutoCAD 窗口实测；磁盘陈旧版本检测只覆盖同一交付根目录（`outputs\`）下的同级目录，把新 DLL 拷到别处时无法据此判断。本项保持未勾选，等用户在图形界面确认 0.2.4.0 指纹与两种替换流程。
  - 复测报错定位（2026-09-23）：用户再次截图“翻译或写入失败，图纸未修改：翻译服务响应不是有效 JSON，图纸未修改。”。按 UTF-16 逐字节复核 `outputs` 下全部交付 DLL：该文案只存在于 preview2、preview3、preview4；第一版 preview 本身不含这句，preview5 起换成“响应内容不是有效 JSON…”。本机 `NETLOAD` 最近记录仍指向 `outputs/StructuralToolbox.DrawingTranslation_preview2_AutoCAD2020`，因此报错来自会话里残留的旧程序集，不是新版本翻译失败；图纸未被修改。（同时更正前面“该文案只存在于 preview、preview2…”的表述：第一版 preview 不含这句。）
  - 接口自检命令（2026-09-23）：新增 `STPVTRANSLATETEST`：不选对象、不改图纸，把 `北立面支架`、`屋面组件数量` 两条示例发给当前配置接口，命令行打印接口、模型、目标语言、两条译文和耗时；命令入口在 accoreconsole 里可执行，补上 WinForms 流程无法脚本化的短板。成功写日志 `STPV_TRANSLATE_SELFTEST_PASS samples=2 first=North facade support`，失败写 `STPV_TRANSLATE_SELFTEST_FAIL ...`；程序集升到 0.2.5.0。
  - 交付与验证（2026-09-23）：交付 `outputs/StructuralToolbox.DrawingTranslation_preview9_AutoCAD2020`（程序集 0.2.5.0，84992 字节，SHA-256 `B45DD60225016B3D0ED846E9A15C421E65A3563E32C86C05CA328EEA522BC382`），README 与 SHA256SUMS 同步。门禁 `scripts\test-drawing-translation.ps1` 全绿；`scripts\verify-drawing-translation-guards.ps1` 按 0.2.5.0 / preview9 重新标定后 26 项全 PASS；新增 `scripts\verify-drawing-translation-api.ps1` 在 accoreconsole 里对交付 DLL 跑 `STPVTRANSLATETEST` 全 PASS（真实接口 `https://cpa.imfourj.cn/v1/chat/completions`、模型 deepseek-v4.1-flash、samples=2、first=North facade support、用时 2.6 秒、`模板图.dwg` SHA-256 未变）；源码级实机回归 `-Mode both` 替换 8/8、插入 8/8、一次 `UNDO` 还原 8/8、接口 `models=8`。用户需完全退出 AutoCAD 后重新 NETLOAD preview9 并用 `STPVTRANSLATEINFO` 确认 0.2.5.0；确认前本项保持未勾选。
  - 旧版本来源定位（2026-09-23）：确认本机不存在自动加载旧 DLL 的机制——`D:\cad\AutoCAD 2020\ACAD.lsp` 是 0 字节空文件；`D:\cad\AutoCAD 2020`、`D:\cad\AutoCAD 2020\Support`、`%APPDATA%\Autodesk\AutoCAD 2020\R23.1\chs` 下的 `*.lsp`/`*.fas`/`*.scr`/`*.dvb` 没有任何一处引用 `STPVTRANSLATE` 或 `DrawingTranslation`；注册表各配置的 `TrustedPaths` 不含交付目录；`C:\Program Files\Autodesk\ApplicationPlugins\ZhStructureToolbox.bundle\Contents\Windows\Legacy\R23.1` 里没有翻译模块；全盘搜索 `StructuralToolbox.DrawingTranslation.dll` 只命中项目 `outputs\`、`work\`。结论：用户会话里跑的是同一会话中先注册的旧程序集加 `NETLOAD` 历史记录（原本指向 preview2）。防复发措施：`outputs\` 顶层只保留 preview9，preview、preview2 … preview8 已归档到 `outputs\archive\`，并把 `%APPDATA%\Autodesk\AutoCAD 2020\R23.1\chs\Recent\NetLoadDialog\StructuralToolbox.DrawingTranslation.dll.lnk` 指向 preview9；守卫脚本不再依赖归档目录，改为在脚本内现场编译一份同名旧版替身，用来复核“旧版会话里再 NETLOAD 新版被静默忽略”场景。
  - 门禁复跑（2026-09-23）：守卫脚本改为自包含后复跑 26 项全 PASS；`scripts\test-drawing-translation.ps1` 全绿（核心 6 项、诊断 36 项、API 测试、模块构建、UI 布局测试、UI 交互 6 场景）；`scripts\verify-drawing-translation-api.ps1` 真实接口自检全 PASS（日志 `STPV_TRANSLATE_SELFTEST_PASS samples=2 first=North facade bracket 用时 3.2 秒`，`模板图.dwg` SHA-256 未变）。为避免再误加载旧程序集，preview、preview2 … preview8 已从 `outputs\` 移入 `outputs\archive\`（本地保留、不进 git），`outputs\` 顶层只剩 preview9，NETLOAD 历史记录里的旧路径已失效。
  - 完善交付（2026-09-24）：版本升至 preview10 / 0.3.0.0；设置面板新增术语表、可选译文缓存、缓存有效期和清空缓存，确认窗新增批量进度与取消；命令行新增按对象类型汇总的跳过原因，并支持非外部参照非常量块属性。交付目录为 `outputs\StructuralToolbox.DrawingTranslation_preview10_AutoCAD2020`，DLL SHA-256 为 `8AFE1CC84CEB209838777DFDCB900D59D6C9B88DFD21EC5EDADFFD24043991DF`。
  - 完善验证（2026-09-24）：`scripts\test-drawing-translation.ps1` 全绿；preview10 守卫 26 项全 PASS；真实 API 自检 PASS；`scripts\verify-drawing-translation-live.ps1 -Mode both` 通过替换 8/8、插入 8/8、一次 UNDO 还原 8/8，原始模板 SHA-256 未变化。ISSUE-0035、0036、0037 已完成；ISSUE-0034 仍需用户在 AutoCAD 图形界面完成设置保存、确认后替换和 DPI 最终验收。

- [x] `ISSUE-0029` 自动更新优先读取 VPS 清单，VPS 返回有效但过期的版本时不会继续读取 GitHub，导致已安装 `v1.0.3` 被错误提示为最新版本。
  - 状态：已完成；从 `v1.0.4` 起只从 GitHub Release 更新，停止使用和部署 VPS 更新站点。
  - 来源：用户于 2026-09-14 要求建立 GitHub 版本发行链；2026-09-17 实机截图确认 VPS 清单仍为 `v1.0.1`、GitHub 清单为 `v1.0.3`，并明确要求“不再使用 VPS 更新，在线更新直接从 GitHub Release 拉取”。
  - 当前根因：`UpdateSourceResolver.ResolveAll()` 按 VPS、GitHub 排序，而 `UpdateService.Check()` 将第一个可访问且有效的清单视为权威结果；VPS 返回旧清单不属于连接失败，因此不会回退到 GitHub。
  - 定版方向：客户端只读取公开 GitHub Release 的 `latest/download/stable.json`；稳定清单的安装包主地址直接指向同版本 GitHub Release 资产，不生成 VPS 地址或镜像，不接受 SSH 部署参数。继续执行 HTTPS、大小、SHA-256、超时、取消和原子下载校验。
  - 迁移限制：已发布的 `v1.0.3` 客户端仍内置 VPS 优先逻辑，无法通过代码外的 GitHub 清单绕过有效的旧 VPS 清单；用户需要手动安装一次含新逻辑的版本，此后版本可完全通过 GitHub 在线更新。
  - 验收要求：客户端正式清单源只有 GitHub `latest/download/stable.json`；发行清单的 `url` 为 GitHub 版本资产且 `mirrors` 为空；发行脚本只准备和发布 GitHub 资产；更新、发布、完整回归、候选安装器、AutoCAD 2020 和 GitHub 下载回验通过；不允许 HTTP 降级或跳过大小、SHA-256 校验。
  - 已完成部分（2026-09-16）：公开发行仓库已发布 `v1.0.3`，包含安装器、`stable.json`、`sha256.txt` 和 `release.json`，发布脚本下载回验通过。安装器 SHA-256 为 `858e58bd196960c2129293bbaba23562303b2c58d56a13accd0eb9eee562ed4f`，源码 tag `v1.0.3` 指向 `c3ee2f7`。
  - 实施进展（2026-09-17）：客户端已删除 VPS 清单地址，只保留 GitHub `latest/download/stable.json`；发行准备脚本不再生成 `vps/` 目录，发行脚本不再接受 SSH 部署参数；18 项 GitHub 单源专项测试和完整 `scripts/test.ps1` 回归通过。待完成补丁版本候选、AutoCAD 2020、安装器和 GitHub 正式发行回验后关闭本问题。
  - 发行进展（2026-09-17）：`v1.0.4` 候选、安装器隔离运行、AutoCAD 2020 五模块加载和本地正式输出已通过；正式安装器 SHA-256 为 `B213814E334A62108D2BE13CD428C45F31E4EDF77592298D80D23A202D3B20D2`。
  - 完成日期：2026-09-17。
  - 最终验证：`v1.0.4` 已发布为 GitHub latest Release，非草稿、非预发布；线上 `stable.json` 为 `1.0.4`，安装包地址直接指向 GitHub 版本资产且 `mirrors` 为空；安装器、`stable.json`、`sha256.txt`、`release.json` 四个资产全部下载回验，远端安装器为 `2209879` 字节，SHA-256 与正式输出一致。Release：`https://github.com/deepfry666/SijiPVToolbox-Releases/releases/tag/v1.0.4`，源码 tag `v1.0.4` 指向 `6ffd98d042109b2bbecb172050cd787c76ec7c91`。

- [x] `ISSUE-0031` 正式安装器与真实图纸验证器计算源码指纹时使用的模块范围不一致，导致已通过安装器校验的候选无法进入真实图纸验证。
  - 状态：已完成；安装器构建、制品校验、证据和归档均已把链接编译的阵列识别源码纳入指纹。
  - 来源：2026-09-17 验证 `v1.0.4` 正式候选时，`verify-real-drawing.ps1` 在运行 AutoCAD 前按门禁中止并报告 `Current StructuralToolbox source fingerprint differs from CandidateBuildResult`。
  - 当前根因：`build-real-drawing-verifier.ps1` 和完整回归把 `src/StructuralToolbox.ArrayRecognition` 纳入正式源码指纹，因为配重模块链接编译其中的识别核心；`package-installer.ps1` 和安装器制品校验只包含壳、Contracts、Rail、Ballast、Update 五个目录，遗漏该实际编译输入。
  - 验收要求：安装器构建、制品校验、真实图纸验证、发布证据和归档使用同一组源码根目录；重建候选后源码指纹一致，真实图纸验证通过；不改变正式五 DLL 交付边界。
  - 完成日期：2026-09-17。
  - 验证：完整回归中的发布追溯测试由 374 项增至 378 项并通过；重建候选的源码指纹统一为 `CBE931ABE5D131F12D0F66B29D41044530F54D5DA352E4ACCEE2EFECD3229DA4`；内部验证程序集构建门禁通过，不再报告候选指纹不一致；用户明确 DWG 仅为测试材料，本次不追加真实图纸业务验收。

- [x] `ISSUE-0030` 配重统计无法统计用户单独增加或复制的单个墩子，导致“工具生成”和“选定范围全部”两种模式对实际墩子数量都不完整。
  - 状态：已完成；生成继续保持一榀一个外层块，统计统一进入榀块逐个识别单墩，并直接识别模型空间中的标准单墩或已点选标记的手画墩。
  - 完成日期：2026-09-16。
  - 现象：自动生成时一个外层配重榀块包含一列嵌套单墩块；统计能从带 `BALLAST_TOOL` 元数据的外层配重榀拆出嵌套数量，但用户单独复制、插入或手动画出的单墩块没有被稳定识别，统计结果漏项。
  - 当前根因：`STPVBALLASTSTAT` 先按外层 `BlockReference` 的 `BALLAST_TOOL` XData 识别整榀，再读取其块定义中的嵌套墩子；对其他对象只支持当前图层上的 `Circle` 和闭合四边形 `Polyline`。自动生成的单墩块本身是无 XData 的 `BlockReference`，因此既不是整榀，也不是现有的“松散圆/方墩”类型。`工具生成(T)`还会主动跳过所有非整榀对象；`选定范围全部(A)`虽然名称表示全部，但实际仍限制为配置图层及上述有限对象类型。
  - 影响：统计模式选择不能改变漏统计问题；生成数量、后续手动补墩和最终工程清单之间可能不一致，表格的规格分组也无法反映单个复制墩子的真实规格。
  - 定版方案：生成结果继续保持“一榀一个外层块”，便于整榀选择、移动、删除和重新生成；外层块只承担容器与关联管理职责，不再作为数量统计单位。榀内每个墩子均引用带稳定单墩身份的标准块定义，单墩身份记录规格、形状、高度和默认颜色；外层榀继续记录生成批次、组件阵列、逻辑列号、原始位置与变换。
  - 识别规则：统计遇到工具配重榀时进入其块定义，逐个识别嵌套的标准单墩引用，并通过外层 `BlockTransform` 计算每个墩子的实际中心和方向；遇到模型空间中的同类单墩引用时直接计为一个。统计结果统一转换为单墩实例后按对象路径去重和分规格汇总，不采用外层榀元数据中的预存数量。
  - 复制规则：复制整榀增加该榀实际包含的全部单墩；从榀内复制或 `NCOPY` 出来的标准单墩，因为引用同一个已识别的单墩块定义，直接计为一个新墩。不同实体即使位置重叠仍分别计数，并报告重叠提醒。
  - 手动画墩：增加“点选识别单墩”操作，允许连续点选圆、闭合矩形或已有块参照；程序读取可确认的平面尺寸，并由用户确认匹配的现有配重规格后，把同样的单墩元数据写入所选实体。识别成功后，该对象及其后续复制对象与工具生成单墩使用同一统计逻辑；无法确认形状或规格时明确拒绝并说明原因。
  - 统计交互：框选统计扫描范围内的工具配重榀和顶层已识别单墩；对榀内对象按单墩中心是否位于框选范围内决定是否计入，因此框线只覆盖一榀的一部分时只统计范围内的墩子。主流程只保留“框选已识别单墩”；未标记的手画对象先通过点选识别，不在统计时按图层或相似外形静默猜测。
  - 旧图兼容：旧版工具榀继续作为容器读取，但必须逐个验证其嵌套块确为配重单墩；能通过既有 `PZ_圆_...`、`PZ_方_...` 块名和块内几何唯一确认的旧单墩正常计入，无法确认的对象明确列入未识别清单，不需要展开或改写旧榀。
  - 验收要求：新生成的一榀含 `N` 个墩时统计为 `N`；复制整榀后统计为 `2N`；从榀内复制 `K` 个标准单墩到模型空间后统计为 `N+K`；点选识别的手画墩及其复制件均正确计数；部分框选只计入中心位于范围内的单墩；逻辑列信息、冲突显示、查看、删除、重新生成和人工修改保护保持有效；覆盖新旧榀、旋转、移动、重叠实体、保存重开、取消和一次 `UNDO`，且不得改写根目录原始 DWG。
  - 验证：配重专项 16 项布局测试、工程量统计测试和 4 项单墩身份解析测试通过；完整 `scripts/test.ps1` 回归通过，其中含 24 项工具箱 UI 布局、15 项模块隔离、374 项发布追溯和 60 项发布工作流测试。AutoCAD 2020 Core Console 实测一榀内 10 个单墩、独立标准单墩 1 个、点选标记手画圆墩 1 个，统计表合计 `12 = 10 + 1 + 1`，保存重开后仍为 12；证据目录为 `work/ballast-pier-autocad/20260916-162020/`。

- [x] `ISSUE-0052` 正式安装器发布脚本把已经纳入版本管理的 `outputs/update-release-v<版本>` 目录判定为不支持条目，导致新版本无法在保留 GitHub/VPS 发布材料的同时写入正式安装包目录。
  - 状态：已完成；发布白名单增加严格的三段版本更新发布目录规则，既有安装器归档逻辑不会移动或改写这些目录。
  - 来源：当前任务的正式发布验证。
  - 登记日期：2026-09-16。
  - 完成日期：2026-09-16。
  - 根因：`Publish-TestRelease.ps1` 的 `outputs/` 显式白名单早于 GitHub/VPS 版本化发布目录，尚未包含 `update-release-v<三段版本>` 命名规则。
  - 验收要求：只允许严格匹配 `update-release-v数字.数字.数字` 的目录与既有安装器、归档、独立模块目录共存；相似但不合规的目录仍应拒绝；发布原子性回归和完整回归通过。
  - 验证：21 项发布原子性测试通过，覆盖发布失败回滚与成功替换时更新发布目录内容及 SHA-256 均不变化；完整 `scripts/test.ps1` 通过。

本文件是四季光伏工具箱跨任务、跨对话的问题台账。用户反馈的问题、用户要求 Agent 整理的问题，以及 Agent 在当前任务范围内确认的可行动问题，都在这里持续保留。

## 状态约定

- `- [ ]`：尚未修补完成，或修补后尚未通过相关验证。
- `- [x]`：修补已经完成，并且相关验证已经通过。
- 状态为“已取消”或“已暂停”的未勾选项不属于当前开发队列，不自动恢复。
- 每个可独立验证的问题使用一个稳定编号；重复反馈更新原条目，不重复建项。
- 完成项只勾选并补充完成日期与验证依据，不删除。

## 问题列表

- [x] `ISSUE-0017` 为自动配重块增加可复核的完整组件阵列识别与阵列清单，并将识别能力建设为可供正式模块复用的独立模块。
  - 状态：已完成；独立模块、自动配重块共享核心接入、阵列清单、过期保护、UI 示意和发布追溯边界均已实现，自动画导轨保留后续切换接口，本轮未替换其生产识别流程。
  - 来源：用户提供 `组件阵列统计.lsp` 和阵列统计表示例，要求两个正式模块具备阵列识别，并优先在自动配重块中增加完整阵列识别和清单表格。
  - 登记日期：2026-09-03
  - 完成日期：2026-09-03
  - 当前基线：自动配重块已有闭合轻量多段线的单次框选、完整矩形阵列校验及配重块统计，但阵列识别结果只用于配重生成；界面不显示完整 `M×N` 规格汇总，也不能输出组件阵列统计表；现有识别不支持块参照、多方向样本组和多次框选。
  - 口径：`M` 为沿组件长边方向的组件数量，`N` 为沿组件短边方向的组件数量；统计表按 `M`、`N` 排序，包含阵列规格、单阵列组件数、相同阵列数量、组件总数，以及正确识别和未正确识别合计。
  - 安全边界：阵列表与配重块表必须分开；识别和取消不得修改组件；插表为一次独立 `UNDO`；不得静默忽略不支持对象；不得递归修改 Xref 或嵌套块；不得覆盖项目根目录原始 DWG。
  - 验收要求：
    1. 独立阵列识别模块和命令可构建、独立加载和测试，支持顶层闭合二维多段线及块参照、多方向阵列、多次窗口选择与对象去重。
    2. 按可配置外轮廓断开净距 `G` 分组，完整 `M×N` 网格正确计量；缺块、错位、重复位置、混合标高或不支持对象明确报告数量和原因。
    3. 自动配重块复用同一识别结果完成按排设置、特殊阵列覆盖、生成配重和阵列清单；组件对象变化后不得继续使用过期结果。
    4. 阵列统计表字段和用户示例一致，固定样例 `272` 个正确阵列、`1854` 块组件逐行汇总正确；`1×1` 按有效阵列计入。
    5. UI 中 `G` 的外轮廓净距含义有同屏示意图；约 `340 px` 工具箱宽度及 100%、125%、150%、200% DPI 下按钮、菜单、汇总和示意图无裁切或重叠。
    6. 在 `work/` 图纸副本中通过 AutoCAD 2020 的识别、插表、保存重开、取消和一次 `UNDO` 验证；独立验证稳定后再进入正式安装包。
  - 当前交付：`outputs/StructuralToolbox.ArrayRecognition_v0.1.0_AutoCAD2020`；独立命令 `STPVARRAYRECOGNIZE`、`STPVARRAYTABLE`；自动配重块新增 `STPVBALLASTARRAYTABLE`，正式 `StructuralToolbox.Ballast.dll` 直接编译并复用同一识别核心，不增加运行时 DLL 依赖。
  - 验证：阵列核心 11 项、配重核心 16 项、工具箱 UI 24 项、模块隔离 15 项、发布追溯 374 项及完整 `scripts/test.ps1` 通过；固定样例 `222×(7×1)+50×(3×2)=272` 个阵列、`1854` 块组件逐行汇总通过。
  - AutoCAD 2020：`work/array-recognition-autocad/20260903-151722/result.json` 为 `PASS`；两次重叠窗口覆盖多次框选和去重，取消前后对象数 `7→7`；独立阵列表一次 `UNDO` 为 `0→1→0`，配重页阵列表为 `1→2→1`；生成 `4` 个配重榀，保存重开后保持 `2` 张独立表和 `4` 个配重榀。验证阵列 DLL、配重 DLL、图纸 SHA-256 分别为 `E7A9C35A0AA8D8291F1CDF12B2C03D61CEC1DB7EE69CB6957D67729E2FA787F2`、`F26B6802FF11EC6ADA477BA356E9ECB354B88C9D29C4668E6F3D9E1FC5AC4039`、`E8E8A266C212196EE2029CC5C1C31AF87E61FA616A57658E1F7175A78A858F47`；验证图纸始终位于 `work/`，根目录 `模板图.dwg` SHA-256 保持 `9891A6CA9AF691413337E11AADCE840041EF50DACE017BDCC7DC0EC2D9BB4917`。

- [ ] `ISSUE-0016` 正式安装包中的 .NET Framework DLL 未做混淆，安装后可被常见 .NET 反编译工具较容易地恢复类型、方法和业务逻辑结构。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户指出“这个很容易被逆向”。
  - 登记日期：2026-09-02
  - 当前基线：正式发布仅执行 Release/x64 优化；发布和安装脚本中没有程序集混淆、控制流处理、符号重命名或字符串保护步骤；单 EXE 由 Inno Setup 使用 LZMA2 压缩五个正式 DLL，安装后 DLL 原样写入 AutoCAD 标准 `.bundle` 目录；EXE 与 DLL 当前均无 Authenticode 签名。
  - 风险边界：混淆只能提高分析和复制成本，不能保证 .NET 程序不可逆向；代码签名用于来源和完整性验证，不会阻止反编译；安装器加壳或改压缩方式也不能保护安装后必须由 AutoCAD 加载的 DLL。
  - 方案约束：不得破坏 AutoCAD 命令发现、`IExtensionApplication` 入口、模块反射发现、公共 Contracts 接口、程序集版本读取、设置/元数据兼容、异常诊断、在线更新校验以及 AutoCAD 2020 Win64 加载；保护规则必须可复现并纳入现有发布脚本，密钥不得提交仓库。
  - 验收要求：
    1. 先定义需保护的核心业务程序集、公开 API 保留清单和可接受的性能/启动开销，避免对所有符号盲目处理。
    2. 用固定版本的混淆工具生成独立候选产物，并证明常见反编译结果中的内部类型、方法和核心业务流程可读性显著下降；不得宣称“无法破解”。
    3. 现有自动化回归、发布追溯和安装器验证全部通过，且混淆后候选的哈希和工具/规则版本进入构建回执。
    4. 在 `work/` 图纸副本中完成 AutoCAD 2020 启动自动加载、`JJJJ`、全部正式模块命令、保存重开、一次 `UNDO`、升级和卸载验证。
    5. 单独评估 Authenticode 签名，用于降低未知发布者提示并验证发布者和文件完整性，但不得把签名当作防反编译措施。

- [ ] `ISSUE-0015` 将用户提供的 `导轨对齐标注(3).lsp` 整理为可独立测试和交付的导轨标注方案。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户提供现有 LSP 并询问“这个能不能给我做成导轨标注的方案”。
  - 登记日期：2026-09-02
  - 现有脚本基线：命令 `GDBZ`；先选示例导轨和示例组件，再支持多次矩形框选；按图层、可见颜色、实体类型和组件轮廓筛选；按组件阵列及导轨长度选择代表导轨；动态预览后生成原生对齐/旋转尺寸；优先使用 `TSSD_100_100`、`100:100` 或 `100：100` 标注样式。
  - 独立范围：保留兼容命令 `GDBZ`，增加工程化命令名；源码放在 `src/`，构建与验证脚本放在 `scripts/`，临时文件只放 `work/`，可交付文件只放 `outputs/`；不得覆盖根目录原始 DWG。
  - 安全边界：取消或报错不得残留尺寸，不得修改导轨和组件；一次 `UNDO` 撤销本次生成的全部尺寸；缺少首选标注样式时必须给出明确降级结果；重复运行不得静默删除既有人工标注。
  - 当前交付：`outputs/StructuralToolbox.RailAnnotation_v0.1.0_AutoCAD2020`；主命令 `STPVRAILANNOTATE`，兼容命令 `GDBZ`；仓库源文件为 UTF-8，交付 LSP 为 GBK 代码页 936。
  - 当前验证：16 项源码/编码/独立边界门禁和完整 `scripts/test.ps1` 回归通过；最终 `outputs/` GBK LSP 在 AutoCAD 2020 中通过对象与几何检查累计 38 项；生成 6 个原生尺寸后一次 `UNDO` 计数由 `6` 归零，再次生成、保存重开仍为 `6`；30° 旋转尺寸、竖向对齐尺寸、样例不匹配和多窗口去重均通过；原始 `acad.dwt` SHA-256 前后均为 `D52A38420C0DFAE2B3EF7B2628D6B4B9ACB7906158BFFBA881770F0367B30E1A`。详见 `docs/导轨对齐标注_v0.1.0_验证记录.md`。
  - 验收要求：独立源码和交付物可复现；覆盖水平、竖向、旋转、多组件阵列、多窗口合并、样例不匹配、取消和错误恢复；在 `work/` 图纸副本中完成 AutoCAD 2020 加载、原生尺寸创建、保存重开及一次 `UNDO` 验证；通过后再勾选，不把未验收功能并入主工具箱。

- [x] `ISSUE-0014` 配重页底部按钮在紧凑工具箱宽度下被压缩并裁切，右侧“框选统计”几乎不可见。
  - 状态：已完成；底部操作栏补齐 4 列，识别前隐藏的“重新识别”列宽为 0，识别前后按钮按可用宽度动态分配。
  - 来源：用户安装 `v1.0.0` 后对比导轨页和配重页截图反馈“导轨界面正常，配重块的按键怎么缩起来了”。
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 受影响版本：四季光伏工具箱 `v1.0.0`。
  - 截图证据：导轨页 `345B7140807B6E62A519CDA0B6BF2B9D72EE792D9E52D4439A97246A9B82C90C`（`345 x 575`）；配重页 `C5513ED036DCF248490985DFEE30BA82D60F6F6C748B6FEA2555E254670EB59F`（`339 x 571`）。
  - 验证：工具箱 UI 回归 23 项通过；约 `340 px` 宽工具箱在 100%、125%、150% DPI 下识别前 3 个按钮和识别后 4 个按钮均完整位于页面内、文字不裁切；隐藏按钮不占列宽；AutoCAD 2020 Core Console 与安装器静态/隔离运行验证通过。
  - 验收要求：约 `340 px` 宽的工具箱中，识别前 3 个可见按钮和识别后 4 个可见按钮均完整位于页面内、文字不裁切；隐藏按钮不占列宽；100%、125%、150% DPI 布局回归通过。

- [x] `ISSUE-0013` 主工具箱发布脚本把 `outputs/` 中已交付的独立插件目录误判为非法条目，导致 `v1.0.0` 无法发布。
  - 状态：已完成；发布流程只轮换 `SijiPVToolbox_*` 主工具箱版本并原样保留独立插件交付目录，`Published` 就绪检查也只检查主工具箱正式源码范围。
  - 来源：本次发布前检查确认；`StructuralToolbox.Layout_v0.1.2_AutoCAD2020` 是 `ISSUE-0012` 已恢复的独立交付物。
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 验证：19 项发布原子性测试通过，覆盖成功发布与故障回滚后的独立交付物 SHA-256 保持；371 项发布可追溯测试和完整 `scripts/test.ps1` 通过；未接入本次发布的 `StructuralToolbox.PvComponent` 保持未提交时，`Published` 就绪检查仍可通过。
  - 验收要求：发布成功与故障回滚都不得删除、移动或改写独立插件目录；`Published` 就绪检查只约束主工具箱版本唯一性；增加独立交付物哈希保持回归。

- [x] `ISSUE-0011` 从四季光伏工具箱主程序和正式安装包中暂时移除“混凝土屋面支架大样”（自动画支架）功能。
  - 状态：已完成；独立模块源码与历史验证资料保留，`v1.0.0` 主工具箱不再发现、显示或安装该模块。
  - 来源：用户确认该功能当前达不到使用要求，要求先从工具箱中剔除。
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 影响范围：主界面模块发现与导航、主程序构建依赖、Bundle 清单与安装包内容、真实图纸验证的模块数量和发布文档。
  - 验证：正式构建只生成五个 DLL；22 项工具箱 UI 布局测试、371 项发布可追溯测试和完整 `scripts/test.ps1` 通过；AutoCAD 2020 Core Console 返回 `MODULES=5`；安装器全新安装、同版本覆盖、失败回滚、降级拒绝和卸载通过，安装后负载严格为五个 DLL且不含 `StructuralToolbox.SupportDetail.dll`。
  - 验收要求：主界面无支架大样入口；正式 Bundle 不含 `StructuralToolbox.SupportDetail.dll`；其余既有模块仍可发现与加载；程序集、清单、UI/About、README、发布定义及安装器统一为 `v1.0.0`；完成自动化、AutoCAD 2020 和安装器验证。

- [ ] `ISSUE-0010` 将支架大样拆为可独立插入、编辑和计量的智能构件，第一阶段先完成光伏组件。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户认为一键支架大样难以适应实际变化，要求从组件开始逐个完成构件，并保证默认尺寸、自定义尺寸及拉伸后的结果均可进入最终工程清单。
  - 登记日期：2026-08-28
  - 当前定版规则：组件默认尺寸 `L=2382 mm`、`W=1134 mm`、`T=30 mm`；大样中绘制 `L×T` 侧视轮廓，`W` 作为组件数据和清单规格保存；每个组件独立计量为一块。
  - 范围：独立组件模块提供参数界面、同屏 `P0/+X/L/W/T` 示意图、插入、选中回读编辑、信息查看和组件统计；组件保持为可编辑的原生闭合多段线，合法矩形拉伸后按实际几何重新量取，不从旧参数猜尺寸；复制后的实体按 DWG 对象身份独立计数。
  - 验收要求：默认和自定义组件可插入任意位置及角度；合法拉伸后 `L/T` 与工程量统计同步；异常拉伸为非矩形时明确报为无效且不得静默计量；复制、删除、一次 `UNDO`、保存重开及统计表均通过 AutoCAD 2020 的 `work/` 副本验证；不得改动项目根目录原始 DWG。

- [x] `ISSUE-0009` 混凝土屋面支架大样独立模块没有接入四季光伏工具箱主界面，依赖单独 `NETLOAD` 和命令操作，测试与日常使用不便。
  - 状态：已完成，作为第六模块进入四季光伏工具箱 `v0.4.2`。
  - 来源：用户在独立模块完成后反馈“没有操作界面还有点麻烦”，要求直接接到主工具箱并生成相应界面。
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 验证：主壳首页可发现并打开支架页，页面提供新建、编辑、查看和删除四项操作；22 项工具箱 UI 测试覆盖 100%、125%、150%、200% DPI 与紧凑窗口滚动；最终候选在 AutoCAD 2020 中通过支架生成、回读更新、保存、重开及六模块加载验证，重开后关联实体 `64` 个、主图原生实体 `25` 个，主验证标记为 `MODULES=6 RAILS=656 DIMENSIONS_UNCHANGED=True`；安装器全新安装、同版本覆盖、失败回滚和卸载均通过，正式 EXE SHA-256 为 `392BE297C1D6233A0DA7264C6E24F88B1FF8F3231058FDBA423EFF022CA53015`。详见 `docs/安装器验证记录_v0.4.2.md`。
  - 当前基线：独立模块 `StructuralToolbox.SupportDetail v0.1.0` 已通过几何、元数据、UI 和 AutoCAD 2020 验证，但尚未进入主工具箱的模块发现、首页导航、统一操作页面和正式安装包。
  - 范围：在主工具箱首页增加“混凝土屋面支架大样”入口；提供可直接新建、选中回读编辑、查看信息和删除实例的统一页面；自定义夹持位置、墩位、尺寸及高度时同屏显示与字段同名的工程示意图；保留独立命令作为诊断入口。
  - 验收要求：主工具箱可发现并打开该模块页面；页面在 100%、125%、150% 和 200% DPI 下无控件重叠或裁切；安装后无需再次手动加载独立 DLL；使用 `work/` 下 DWG 副本完成主工具箱程序集加载、模块发现和支架生成/保存/重开验证；程序集、清单、首页/About、README 和发布目录版本保持一致。

- [x] `ISSUE-0008` 建立独立、可回读编辑的混凝土屋面支架大样模块。
  - 状态：已完成；首版与导轨、配重块模块保持独立。
  - 来源：用户提出将混凝土屋面支架大样拆分为可自动出图、可调节、可回读的独立模块。
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 验证：10 项几何、3 项元数据、3 项 UI/示意图和 13 项源码约束测试通过；发布包在 AutoCAD 2020 Core Console 中通过生成、回读、更新、保存及重开验证，重开后关联实体 `64` 个，其中主图原生非块实体 `25` 个；验证图为 `work/support-detail-verification/support-detail-check.png`。发布 DLL SHA-256 为 `B9FBE78FA883B42FBA189BC0F01A172E88136CB555D281EE7E83E32424A00E52`，固定详图库验证前后 SHA-256 均为 `E98C1CAACDA833E90F14E09693235C3312F74039F931E8FEE4243CBAB9AADD26`。
  - 范围：任意倾角、可变组件数量和缝隙、可自定义墩子数量/位置/尺寸、可自定义立柱高度和斜梁端部伸出；生成后保留稳定元数据，支持重新读取和局部更新；固定节点大样和材料表作为可复用图纸内容。
  - 约束：斜梁首版为整根直梁；夹持位置可手动输入且默认组件居中、导轨中心距 1400；背拉为套图内容；不得覆盖根目录原始 DWG。
  - 验收要求：独立项目可编译；纯几何规则有自动化测试；生成实体不是锁死整体块，至少可按实例回读参数并更新；自定义几何输入界面同时显示带同名符号的工程示意图；使用 `work/` 或 `outputs/` 副本完成真实 DWG 验证。

- [x] `ISSUE-0001` 建立跨对话的问题登记、修补和勾选闭环。
  - 来源：用户反馈
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 验证：已创建本清单，并在根目录 `AGENTS.md` 中加入每次任务自动维护本清单的项目规则。

- [ ] `ISSUE-0002` 组件自动排布在设置最大容量后，按扫描顺序反复保留各排的单列或短尾阵列，提前耗尽容量并让后续可布置完整阵列的区域留空。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户在 AutoCAD 2020 中测试独立组件自动排布插件时反馈。
  - 登记日期：2026-08-28
  - 当前验证：18 项纯几何回归通过；原单排复现由 `4,4,4,3,1` 修正为 `4,4,4,4`，原双排复现由 `8,2,8,2,8,2,2` 修正为 `8,8,8,8`；覆盖容量不能整除 `M`、无限容量、硬上限和确定性。AutoCAD 2020 在用户测试 DWG 的 `work/` 副本上通过 `0.1.1.0` 命令级诊断。详见 `docs/组件自动排布_v0.1.1_修补与验证记录.md`。
  - 受影响基线：`StructuralToolbox.Layout v0.1.0`，程序集 `0.1.0.0`，插件基线提交 `6543ae7`，命令 `STPVLAYOUT`。
  - 用户现场现象：设置最大容量后，屋面左侧连续多排出现单列组件；同一屋面上部仍有足够空间可放更完整的阵列。用户原话为“最左侧好多单列的组件，明明上面有很多空余”。
  - 固定截图证据：历史附件：ISSUE-0002-capacity-tail-columns.png（原截图未公开，见根目录 OPEN_SOURCE.md）
  - 截图 SHA-256：`4758A122B55C9E82CF7818C39588E0A11EC7220FE811D3CBE47BF3B08D70C67C`
  - 现场图形特征：主要阵列为双排长阵列；多条横向阵列带在同一侧重复出现 `2×1` 单列尾阵列；达到容量后，上方较完整的可布置区域没有继续参与排布。
  - 容量公式检查：`floor(最大容量 kWp × 1000 / 单块功率 Wp)` 本身正确，问题不是容量单位或向下取整错误。
  - 独立复现方法：使用不修改项目源码的临时阶梯屋面算例，保持容量为硬上限并让早扫描区域同时存在完整阵列和尾列，后扫描区域仍能容纳完整阵列。
  - 独立复现结果：
    - 单排容量限制为 16 块时，当前算法输出 5 个阵列，尺寸为 `4,4,4,3,1`；理想同数量方案可使用 4 个完整 `1×4` 阵列。
    - 双排容量限制为 32 块时，当前算法输出 7 个阵列，尺寸为 `8,2,8,2,8,2,2`；其中多个 `2` 是重复的 `2×1` 单列尾阵列。
  - 已确认源码路径：`src/StructuralToolbox.Layout/Core/LayoutEngine.cs` 的 `SolveGridAtOrigin`。
  - 已确认根因：
    1. 求解器按 `uStart`、`vStart` 的固定空间顺序扫描，每发现一个阵列就立即加入结果并占用容量。
    2. 每一排末端只能容纳一列时，该尾列也会在进入下一排之前被保留。
    3. 容量耗尽后立即停止后续空间扫描，因此后面的完整候选阵列没有机会替换已经占用容量的零散尾列。
    4. 最终评分只能比较各扫描原点已经生成的结果，不能在同一结果内部执行“删除多个尾列、换入后续完整阵列”的全局容量重分配。
    5. 当局部垂直方向与世界坐标方向相反时，扫描末端可能显示在图面左侧，因此形成截图中的“左侧单列”。
  - 问题边界：这是容量约束下的候选选择和整齐度问题，不是屋面边界越界、组件功率换算、障碍碰撞或 AutoCAD 显示问题。
  - 修复时必须保持的现有规则：
    - 最大容量仍是硬上限，任何方案不得多放一块。
    - 一个屋面仍只采用一种朝向和一种阵列排数 `M`。
    - 每个输出阵列仍为完整 `M×N` 矩形，不允许缺角。
    - 冲突方案至少增加 2 块、每阵列最多 1 块冲突组件、全屋面冲突比例限制等规则不得放宽。
    - 相同图形和参数必须得到确定结果。
  - 建议修复方向：先生成并收集全屋面互不重叠的候选阵列，再统一应用容量上限；在组件总数相同的前提下优先列数较多、阵列数较少的完整阵列，最后才使用容量余数形成一个缩短尾阵列。
  - 验收要求：
    1. 用户截图对应场景中不得再按多排重复产生单列尾阵列，同时让后续完整区域留空。
    2. 组件总数不降低且不突破容量上限；如果同数量存在更完整的组合，必须选择更完整的组合。
    3. 因容量截断产生的缩短尾阵列最多一个；由真实边界或障碍造成的短阵列必须能证明不存在可替换的更完整候选。
    4. 加入单排 `16 → 4,4,4,4` 和双排 `32 → 8,8,8,8` 的回归测试，并覆盖容量不能整除 `M`、容量留空、无容量限制和相同输入确定性。
    5. 使用用户测试 DWG 的 `work/` 副本完成 AutoCAD 2020 视觉验收，确认数量、位置、阵列分组和一次 `UNDO` 均正确。

- [ ] `ISSUE-0003` 线段式管道障碍无法识别，普通 `LINE` 和开放 `LWPOLYLINE` 即使被用户选中也不会进入组件避障计算。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户在 AutoCAD 2020 中测试独立组件自动排布插件时反馈。
  - 登记日期：2026-08-28
  - v0.1.1 验证历史：对象分类和有宽中心线模型曾通过自动化，但 `B` 参数规则已被用户否定，不再作为有效完成证据。
  - 2026-08-28 用户规则修订：普通 `LINE` 和开放直线 `LWPOLYLINE` 本身即为零宽障碍；不猜测实体管径，不新增 `B`；组件轮廓与开放线的最小距离不得小于 `O`。当 `O=0` 时只阻止实际压线或穿线。
  - 当前验证：`B` 已从设置、界面、求解器和文档删除；22 项核心回归覆盖零宽线实际相交、`O` 退距、折线和多方向线；AutoCAD 2020 诊断标记为 `LINES=PASS`。
  - 受影响基线：`StructuralToolbox.Layout v0.1.0`，程序集 `0.1.0.0`，插件基线提交 `6543ae7`，命令 `STPVLAYOUT`。
  - 用户现场现象：屋面中的管道以线段或开放线串表示，用户选择这些对象作为障碍后，组件排布没有避让。用户原话为“障碍要是这种线段式的管道，不会识别出来好像”。
  - 固定截图证据：历史附件：ISSUE-0003-line-pipe-obstacle.png（原截图未公开，见根目录 OPEN_SOURCE.md）
  - 截图 SHA-256：`D12914854ABAD9C58C0F893FDD0B05B29F2D6F71D0FEF649E464355789C8AD63`
  - 相关本地图纸：`示例工程-B.dwg`。该文件属于用户工作区，当前未跟踪；诊断只读取 `work/` 下副本，不得覆盖、提交或重命名原文件。
  - 当前明确支持的障碍对象：
    - 闭合、正 Z 法向、只含直线段的二维轻量多段线 `LWPOLYLINE`；
    - 半径大于零的圆 `CIRCLE`。
  - 当前明确不进入障碍计算的对象：普通 `LINE`、开放 `LWPOLYLINE`、带圆弧段多段线、块参照 `INSERT`、Xref 内部对象以及其他未显式处理的实体。
  - 只读图纸清点结果：模型空间包含 133 条普通 `LINE`、4 条开放直线 `LWPOLYLINE`、7 条可支持的闭合直线 `LWPOLYLINE`、17 个可支持的圆以及多个块参照。截图发生在 AutoCAD 当前会话中，不能仅凭保存文件绑定到某一个对象 ID，但实体类型构成与“线式管道被忽略”的现象一致。
  - 已确认源码路径：`src/StructuralToolbox.Layout/Commands/LayoutCommands.cs` 的 `PromptObstacles` 和 `TryReadPolygon`；纯几何输入目前只有 `LayoutPolygon` 与 `LayoutCircle`。
  - 已确认根因：
    1. `PromptObstacles` 对所选实体只尝试读取闭合多段线和圆，没有 `Line` 或开放多段线的转换分支。
    2. 线是零宽度对象，而现有碰撞模型需要有面积的障碍；程序没有管道宽度、直径或线缓冲参数，无法从中心线自动得到真实避让范围。
    3. 现有障碍退距 `O` 只对已经识别的多边形和圆生效；未识别的线即使设置 `O` 也不会参与计算。
    4. 块参照和 Xref 不递归展开，因此管道位于块或参照内部时同样无法识别。
  - 工程语义修订历史：v0.1.1 曾按“管道中心线 + 宽度 B”实现；用户在 2026-08-28 明确否定该解释。当前有效规则以“开放线本身即障碍，仅按 `O` 退距”为准，旧的 `B` 方案仅保留为问题追踪历史，不再适用。
  - 交互要求：同一 UI 上下文的示意图必须直接标出开放线障碍和组件外轮廓到障碍线的净距 `O`，不得再显示或要求管道宽度 `B`。
  - 验收要求：
    1. 直接选择普通 `LINE` 和开放直线 `LWPOLYLINE` 后，程序将所选开放线本身作为障碍。
    2. 单段线、多段连续折线、不同方向、折角、开放端部和多条管道均有纯几何测试。
    3. 组件外轮廓到开放线的最小距离不得小于 `O`；`O=0` 时不得压线或穿线。
    4. 不新增管道宽度输入，不因缺少管径阻止开放线识别。
    5. UI 示意图通过 100%、125%、150% 和 200% DPI 布局测试。
    6. 使用用户测试 DWG 的 `work/` 副本完成 AutoCAD 2020 选择、预览、确认、取消和一次 `UNDO` 验证。
  - 关联问题：选择结果和忽略原因反馈见 `ISSUE-0004`；即使暂不支持块参照或 Xref，也必须通过 `ISSUE-0004` 明确告知用户。

- [ ] `ISSUE-0006` 用户点选屋面轮廓后，插件提示“多段线未闭合”或“屋面边界存在自交”，需要避免对工程图中端点实际重合、连续重复点等可规范化轮廓的误判，并对真正无效轮廓给出准确原因。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户在 AutoCAD 2020 中复测独立组件自动排布插件时反馈。
  - 登记日期：2026-08-28
  - 受影响版本：`StructuralToolbox.Layout v0.1.1`，程序集 `0.1.1.0`。
  - 用户原话：“要么就是未闭合，要么就是屋面轮廓自交”。
  - 证据截图：历史附件：ISSUE-0006-0007-layout-rule-feedback.png（原截图未公开，见根目录 OPEN_SOURCE.md），SHA-256 `657915230A087836ABA3E34D702C852C47531FE1175D09D6B3467DBC51690D68`。
  - 需要核对：`Polyline.Closed` 标志、首尾端点几何重合但未设置 Closed、首尾重复顶点、连续重复顶点、零长度边，以及真正的非相邻边相交。
  - 验收要求：端点在明确容差内重合的开放轻量多段线可作为闭合屋面读取；重复点和零长度边规范化后不误报自交；真正开口和真正自交分别给出不同、可理解的提示；加入纯几何和 AutoCAD 对象读取回归。
  - 当前验证：首尾间隙不超过 `0.1 mm` 的开放多段线按闭合轮廓读取；连续重复点、重复闭合点和零长度边会先清理；真正开口提示首尾相差的毫米值；真正自交仍明确拒绝。22 项核心回归及 AutoCAD 2020 `BOUNDARY=PASS` 通过。

- [ ] `ISSUE-0007` “阵列排数 M”输入的业务含义实现错误：填写 `2` 应表示允许 `1～2` 排参与整屋面求优，而不是把全屋面限制为仅 `2` 排。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：用户在 AutoCAD 2020 中复测独立组件自动排布插件时纠正规则。
  - 登记日期：2026-08-28
  - 受影响版本：`StructuralToolbox.Layout v0.1.1`，程序集 `0.1.1.0`。
  - 用户原话：“阵列排数填2不是限制死2排，而是允许1到2排”。
  - 证据截图：历史附件：ISSUE-0006-0007-layout-rule-feedback.png（原截图未公开，见根目录 OPEN_SOURCE.md）。
  - 当前根因：`LayoutSettings.ToOptions` 将 `Rows` 转换成 `AllowedRows = new[] { Rows }`，因此输入 `2` 只求解双排方案。
  - 修订规则：界面字段改为“最大阵列排数 M”；输入 `M` 时求解 `1,2,...,M`，每个候选方案内部仍只使用一种排数，不在同一方案混排。最终继续按组件数量、冲突、阵列数等既定优先级选择。
  - 验收要求：`M=1` 只计算单排；`M=2` 同时比较单排和双排；`M=3` 同时比较 1、2、3 排；存在单排数量更多的屋面时 `M=2` 必须能选择单排；相同输入结果确定；UI 和示意说明使用“最大阵列排数”语义。
  - 当前验证：设置层将 `M` 展开为 `1..M`；新增 `M=2` 且单排更优时选择单排的回归；100%/125%/150%/200% DPI 通过；AutoCAD 2020 诊断标记为 `ROWS=PASS`。

- [ ] `ISSUE-0004` 障碍选择会静默忽略不支持的对象，用户无法知道实际识别数量、忽略数量和忽略原因。
  - 状态：已取消（2026-09-14）；按用户要求停止推进，此条保留作历史记录，不代表已修复。
  - 来源：在排查用户线段式管道反馈时确认的独立交互缺陷。
  - 登记日期：2026-08-28
  - 当前验证：纯支持、纯忽略和混合分类均由 `STPVLAYOUTCHECK` 验证，混合选择满足“识别数 + 忽略数 = 总数”，标记为 `SUMMARY=PASS`；全部未识别时必须重新选择、明确按无障碍继续或取消，不再静默进入排布。
  - 受影响基线：`StructuralToolbox.Layout v0.1.0`，程序集 `0.1.0.0`，插件基线提交 `6543ae7`，命令 `STPVLAYOUT`。
  - 用户可见后果：用户可以框选或点选多个管道对象，AutoCAD 选择过程表面成功，但排布结果没有避让，且程序不说明这些对象没有被识别，容易让用户误以为已经完成避障。
  - 已确认源码路径：`src/StructuralToolbox.Layout/Commands/LayoutCommands.cs` 的 `PromptObstacles`。
  - 已确认根因：
    1. 障碍选择阶段没有只允许受支持对象的选择过滤器。
    2. 遍历选择集时，仅把可读取的闭合直线轻量多段线和圆加入输入；其他实体直接越过。
    3. 方法只返回继续或取消，不返回“已识别、已忽略、忽略原因”的结构化结果。
    4. 即使所有对象都被忽略，命令仍继续求解并生成一个看似已经考虑障碍的结果。
  - 与 `ISSUE-0003` 的区别：`ISSUE-0003` 解决普通 `LINE` 和开放多段线如何作为零宽障碍并按 `O` 退距；本问题解决所有支持和不支持对象的选择反馈。即使某类对象仍决定不支持，本问题也必须独立修复。
  - 需要报告的最小信息：
    - 用户选择对象总数；
    - 成功识别的闭合多段线数量、圆数量以及以后新增的线式管道数量；
    - 被忽略的 `LINE`、开放多段线、带弧多段线、块参照、Xref 或其他实体数量；
    - 每一类被忽略对象的原因和下一步处理建议。
  - 交互约束：反馈应在障碍选择结束后、正式求解前显示；不得只写入难以察觉的调试日志。全部未识别时必须让用户重新选择、明确选择无障碍继续，或取消命令，不能静默继续。
  - 验收要求：
    1. 纯支持对象、纯不支持对象和混合选择集分别有自动化或 AutoCAD 命令级测试。
    2. 混合选择时，识别数量与忽略数量之和等于选择对象总数。
    3. 每种忽略类型有稳定、可理解的中文原因；重复对象或屋面边界本身的处理也必须计数说明。
    4. 全部对象不受支持时不会直接进入排布；用户明确选择“按无障碍继续”后才可继续。
    5. 取消或重新选择不写入组件实体、不改变原障碍对象，并保持一次 `UNDO` 的既有事务边界。

- [x] `ISSUE-0005` 打开“检查软件更新”页面后，页面高度异常增大并把共享工具箱整体拉长；返回首页后工具箱高度仍不恢复。
  - 状态：已完成；模块页面不再请求改变共享 `PaletteSet` 外框尺寸，更新页改为响应式填充布局，旧的异常拉伸尺寸通过一次性布局迁移重置。
  - 来源：用户在 AutoCAD 中使用四季光伏工具箱时反馈。
  - 首次登记日期：2026-08-27（原 `docs/待修改清单.md` 的 `UI-001`）。
  - 迁入统一问题台账日期：2026-08-28。
  - 完成日期：2026-08-28。
  - 根因与修复：模块导航曾按页面首选尺寸反向修改共享宿主尺寸，更新页又包含会扩张的固定内容布局；现已取消页面切换对宿主尺寸的写入，将布局迁移版本由 `5` 提升到 `6` 并把旧尺寸恢复为 `360 x 580`，更新状态区在可用空间不足时独立滚动。
  - 验证：22 项工具箱 UI 布局测试通过，覆盖首页、更新页和导轨页连续往返 10 次且宿主尺寸不变；完整 `scripts/test.ps1` 通过；AutoCAD 2020 Core Console 返回 `DIMENSIONS_UNCHANGED=True`。
  - 受影响基线：四季光伏工具箱 `v0.4.1`。
  - 用户现场现象：打开“检查软件更新”后，在线更新页面在纵向占用接近整个可见工作区，页面下方产生大面积空白；共享工具箱外框随之被拉长。返回工具箱首页后，首页内容虽然只占上部区域，但外框仍保持异常高度，工具箱整体没有恢复到打开更新页之前的紧凑尺寸。用户原话为“打开软件更新之后界面拉的太长，导致工具箱整体拉长”。
  - 固定截图证据：
    - 历史附件：ISSUE-0005-update-page-stretches-toolbox.png（原截图未公开，见根目录 OPEN_SOURCE.md）：更新页面被纵向拉长，截图尺寸 `796×782`。
    - 历史附件：ISSUE-0005-home-remains-stretched.png（原截图未公开，见根目录 OPEN_SOURCE.md）：返回首页后，共享工具箱仍保持过长，截图尺寸 `780×763`。
  - 截图 SHA-256：
    - 更新页面：`262BFF2AB83D4B9769B14B3A116C29ADFA072C6F7C8348BA167F234820B7A1FA`
    - 返回首页：`0419BB7592EDF58FF8B3FD6D740203A0E2F52D63A139D8CBE2E31C9EA0FCA292`
  - 可见影响：
    1. 更新页面的默认布局不紧凑，空白区域明显大于实际内容需要。
    2. 单个模块页面改变了共享工具箱容器的整体高度。
    3. 页面切换后尺寸状态残留，继续影响首页以及后续打开的其他模块。
    4. 过长的工具箱会遮挡或挤压 AutoCAD 绘图区，降低停靠面板的可用性。
  - 预期行为：进入和离开更新页面不应自动改变用户已经设置的工具箱外框尺寸；更新页应在当前可用空间内紧凑排版，状态/日志区域在空间不足时滚动，返回首页后不应留下由更新模块引起的额外高度或大面积空白。
  - 初步待检查源码范围（不是根因结论）：
    - `src/StructuralToolbox.Update/UI/UpdateCheckControl.cs` 的更新页自动尺寸、停靠方式、固定宽高和状态文本区域布局；
    - `src/StructuralToolbox/UI/ToolboxShellControl.cs` 的模块页面装载、返回导航和页面切换布局；
    - `src/StructuralToolbox/UI/ToolboxPalette.cs` 的共享 `PaletteSet` 最小尺寸、页面请求尺寸和尺寸钳制逻辑。
  - 问题边界：当前反馈针对页面尺寸和跨页面布局状态，不是更新源连接失败或版本检查逻辑问题；截图中的网络失败提示仅是现场状态，不应与布局缺陷合并修复。
  - 验收要求：
    1. 从紧凑首页进入更新页、执行一次成功或失败的更新检查、再返回首页，工具箱外框不得被模块自动纵向拉长。
    2. 首页与更新页连续往返至少 10 次后，容器尺寸不得累积变化，首页、自动画导轨、自动布置配重及更新页互不改变其他页面的布局尺寸。
    3. 更新说明、更新源、检查按钮和状态/日志区域在当前可用高度内完整可用；空间不足时仅内容区滚动，不以增大共享工具箱外框作为适配方式。
    4. 覆盖浮动和停靠状态、窄宽与常用宽度，以及 100%、125%、150% 和 200% DPI；不得出现主要控件裁切、异常横向滚动条或大面积无意义空白。
    5. 在 AutoCAD 实机中比较进入更新页前、进入后和返回首页后的外框尺寸，并保留截图或尺寸日志作为验证证据。

- [x] `ISSUE-0012` 组件自动排布独立插件的 `v0.1.2` 交付目录从 `outputs/` 中消失，用户无法找到可供 `NETLOAD` 的 DLL。
  - 状态：已完成；独立交付目录已恢复，不接入四季光伏工具箱主壳、Bundle 或安装器。
  - 来源：用户反馈“自动排布的插件去哪里了”“没有啊”。
  - 登记日期：2026-08-28
  - 完成日期：2026-08-28
  - 根因：后续正式工具箱输出整理时移除了已提交的独立排布交付目录，但源码和构建脚本仍完整保留。
  - 验证：已重新生成 `outputs/StructuralToolbox.Layout_v0.1.2_AutoCAD2020/StructuralToolbox.Layout.dll`，程序集版本为 `0.1.2.0`，文件大小 `51712` 字节，SHA-256 为 `645319CEF936766B629879484DCCEDC2B80CED5D0F2C34E1A2CDA7689822EB1B`；22 项排布核心测试通过，目录内同时提供 `README.md` 和 `SHA256SUMS.txt`。

