# 进度日志

## 上下文压缩恢复点：P4验收通过，下一步P5（2026-10-16）
- 用户已手动测试P4并确认一切正常、站位修正完毕、受攻击优先级正确；此前也已确认连续释放/插队正常、现间隙合适。Play点击与目视继续由用户负责，不再尝试CLI桌面点击。
- P4已完成布阵/左右布局/头顶状态、一键终结技、移除玩家取消与手选目标、护卫执行时最低HP百分比单疗（含自身，同值注册序，治疗量20）；系统死亡/结束退款与P3 FIFO/预留/播放确认保留。剑前术后特性仅后续预设。
- 最近自动化证据Unity214/214通过，失败0、跳过0，Logs/editmode-rework-p4.xml；用户其后手动调整站位并验收，未将此写成新一轮自动化结果。
- 本轮按用户要求提交P4并将P3（5c0168b）与P4一并推送；具体提交/远端结果以git log/status为准。两份字体缓存不纳入提交、不还原。
- 压缩后先读本节、task_plan.md顶部、战斗设计.md，核对git状态，再推进P5：回合手牌UI、双方格子效果交互、复合意图及内容配平；P6全量/横屏/真机验收仍待。不要重做P3/P4，不恢复取消或手选流程，不自动扩大到角色特性。


## 午前恢复点：P4自动化通过，Play待用户验收（2026-10-16）
- P3已本地提交5c0168b，未推送；用户已确认连续释放和插队正常、现间隙合适。后续Play点击/目视由用户验收，不再尝试CLI桌面点击。
- P4已实施：Setup五格移动/占格交换、我左敌右、重试/下一关恢复我方阵型、头顶状态入口；初始不选角色，战斗点击满条角色直接终结技，无轮盘/手选目标/玩家取消排队，系统失效退款保留。
- 护卫改为治疗执行时最低HP百分比的存活队友（含自身，同比例注册序），量仍20；剑前术后偏好仅后续预设。P3动作时长/间隙与generation机制未修改。
- 用户退出编辑器后已重跑Unity全量：214/214通过，failed=0、skipped=0，退出0；证据Logs/editmode-rework-p4.xml、batchmode-rework-p4.log。此前工程占用退出21已解除，不再阻塞。
- 关键实际代码与git diff --check已核对；本轮未启动图形编辑器或尝试桌面点击。P4未提交/推送，字体缓存保留。
- 用户Play待验：选角色后点上方格子圆盘换位/交换、开战后禁换位、左右与HUD遮挡、点角色/头顶一键开大、最低百分比单疗、重试/下一关阵型保留、原插队不退化。后续P5/P6留下午。


## 当前会话：P1/P2推送与P3（2026-10-16）
- P1/P2已提交c5570e5并成功推送origin/main，未包含两份字体缓存。直连证书/网络失败后，以命令级schannel与既有127.0.0.1:6696代理成功推送；未修改git配置、未关闭证书校验。
- P3代码及逻辑测试完成：BeginActions冻结队列，Advance单动作结算，Confirm(BattleId,Sequence)释放占用；终结技FIFO/预留/取消退款/去重/死亡与目标失效清理，末动作间隙接单与每动作胜负已落地；移除同步EndRound/RunRound/UseUltimate入口。
- Runtime唯一协程按动作反馈完成与可调间隙后确认；手选不暂停，菜单动态展示排队/取消，重开generation隔离。未做P4头顶就绪、布阵与左右反转或P5手牌/复合意图。
- Unity实测211/211通过，failed=0、skipped=0，退出0；证据Logs/editmode-rework-p3.xml、batchmode-rework-p3.log，新增31项边界用例。
- 实际进入Play并截图Logs/p3-play-start.png、p3-play-action.png；启动显示正常，日志未见游戏异常。自动桌面点击坐标未可靠命中，未完成逐动作播放/插队/取消/重开操作，不能算交互验收通过；已正常关闭本次编辑器。
- P3未提交/未推送，新增BattleTestDriver.cs及meta未跟踪，字体缓存保留。下一步P4前先手动Play验证P3交互，或继续P4时联合验收；下方P2/昨天记录为历史。


## 当前会话：战斗改版 P2 恢复（2026-10-16）
- 已接通单动作结果/不可变快照/跨轮递增序号、实际伤害与护盾命中记录、动作完成后去重充能；正常行动仅自动普攻，独立终结技不占普攻。删除旧技能能量、手动普攻计划，Runtime最小适配终结技菜单和“开始行动”。
- 首轮Unity实测171项，164通过、7失败，均为角色JSON加载失败：JsonUtility将缺省内联damage/heal实例化为空对象，新的互斥校验误判。已在Runtime加载边界规范空对象并补9项真实JSON加载用例；最终Unity全量180/180通过（失败0、跳过0，退出0）。P2逻辑/配置/测试完成，Play交互待验。
- 已实际启动编辑器进入Play并截图：战场/中文/终结技条/开始行动可显示，无游戏异常；自动桌面点击未可靠触发按钮，未验自动行动/终结技交互，不计Play验收通过。证据Logs/p2-play-initial.png、p2-after-action.png、editor-rework-p2-play.log。已正常退出本次编辑器。
- P3 FIFO/预留/逐动作播放、P4布阵及我左敌右、P5手牌与复合意图均未实现；当前终结技仅在策略窗口即时使用。
- 未提交、未推送、未还原；两份字体缓存因Play继续写回，保留。最终测试证据将写Logs/editmode-rework-p2-final.xml。


## 收工确认（2026-10-15）
- 用户下机，本轮工作停止；已启动的实现/测试/诊断任务均已返回，无需保持终端运行。
- 全部改动保存在本地工作区，未git提交/推送；包含新增未跟踪文件，不能git clean或只复制已跟踪文件。
- 下次可直接说“读取进度，继续P2”；先接完已删除旧资源字段的调用链、恢复编译，再跑完整测试。
- 当前现行设计见《战斗设计.md》；详细阶段见task_plan.md；下方恢复指针记录未完成文件和测试证据。

## 离开前恢复指针（2026-10-15）
- 用户要求本轮只验证API并更新进度，不继续P2。无提交、无推送、无资产还原。
- P1完整落盘并在进入P2前实测134/134通过；证据Logs/editmode-rework-p1-final.xml。
- 当前工作区包含P1与部分P2：P2仅ActionResult.cs/meta、CharacterConfig、Unit、3角色JSON已迁移；旧BattleEngine/SkillResolver/UnitView/测试还引用已移除能量字段，因此当前预计编译不完整，不要将P1绿测当成当前树已绿。
- 下次获准继续P2时，先读task_plan.md/本页/findings.md，再接完动作命中/统一充能/自动普攻与调用迁移；不能重复派发嵌套代理、不能覆盖已保存改动。
- 两份Fonts SDF资产为本次测试写回缓存，仍保留；需要处置时先查diff并确认，不按旧历史日志直接checkout。

## API连接排查（2026-10-15，项目实现暂停）
- 用户切换代理节点后，15:04开始观测：最新实际请求200；15:03:42–51曾有三次error sending request，可能是切换期，无法确认切换精确时间。
- 经现有CC Switch localhost:15721通道连续3次短流式诊断全部HTTP200，均包含message_start/message_stop，无error事件，耗时2938/2447/1796ms。
- 经6696代理访问上游的未认证GET仍403 Cloudflare challenge，但实际认证转发流成功，故不能将这个403直接当真实API故障证据。
- 后续验证完成：长输出约24.7秒、162203字节SSE，正常end_turn；工具请求2.15秒正常tool_use，工具结果往返1.89秒正常end_turn。三条均HTTP200、message_start/message_stop完整、error事件0、不可解析事件0，最大相邻数据块间隔255/128/87ms。
- 截至15:07:28，15:04后CC Switch日志20条请求全部200，最后一条502仍为15:03:51。结合短流3次通过，本轮未再复现断流，换节点后表现改善；尚未验证数分钟流、超长上下文及高并发，不能宣称永久根治。
- 未改配置/重启/切模型，未发送项目源码作诊断输入，仅使用合成文本。API验证本轮结束，P2按用户要求保持暂停。

## 当前会话：战斗改版 P2（2026-10-15）
- 状态：paused。用户要求P1后优先解决API断流，P2作者已确认停止。已保存ActionResult.cs/meta、CharacterConfig、Unit与3角色JSON；BattleEngine/SkillResolver/UnitView/测试尚未迁移旧字段，当前预计不能编译。不得把P1的134/134当成当前部分P2树的测试结果；保留未完成改动，不还原。
- 首轮充能配置采用剑士/法师攻击+1上限3，护卫受击+1上限2，属于可调初值。暂不实现P3插队和P4最终头顶UI。
- 本阶段采用单作者，避免嵌套代理带来的消息与文件协调开销；完成后主会话核对diff并跑Unity测试。

## 当前会话：战斗改版 P1（2026-10-15）
- 状态：complete（Core/配置/测试；界面视觉留P4实测）。五格模型、格子嘲讽与临时效果、最高值并列随机、配置迁移、攻击执行时选目标已落地。
- 两轮Unity实测133/133、补RoundEndTick集成后134/134全绿；最终结果Logs/editmode-rework-p1-final.xml，进程退出0。未启动Play，布局仍我右敌左，左右反转及布阵交互将在P4实施。
- 已移除角色权重与敌方预锁路径（含蓄力），新增FormationConfig资源100/50及明确槽位，默认普攻避免双抽Rng。字体缓存维持未还原、无提交。
- 正在实现Core五格占位/格子嘲讽/最高值目标选择及配置迁移，清除敌人意图和蓄力预锁目标；同步测试与最小Runtime调用，暂不提前交付布阵UI或终结技调度。
- P1完成代码后再运行Unity全量测试，未验证前不标记完成。
- 中途实现代理两次遇API流解码错误退出，主会话已接手Core整合并联系现有Runtime/JSON与测试作者收口，未再增加实现代理。已修复默认普攻计划与伤害结算重复抽目标导致的随机数多消耗；等待调用方整合后统一编译测试。

## 当前会话：战斗改版 P0（2026-10-15）
- 状态：complete（基线已核验，字体缓存处置待确认）。用户已确认改版规则与分阶段计划，已完成文档落盘/改版前基线，尚未修改战斗代码。
- 已更新task_plan.md顶部执行阶段与《战斗设计.md》现行规则，findings.md记录代码约束；下方旧快照和阶段日志仅作历史，不能用于执行旧远程重置或擅自还原资产。
- 新增要求：我方左敌方右、五格战前布阵、头顶终结技就绪提示；其他已确认玩法详见当前设计。
- 基线实测：2026-10-15全量EditMode **78/78通过**，failed=0、skipped=0；结果Logs/editmode-rework-baseline.xml，日志Logs/batchmode-rework-baseline.log，进程退出0。
- 测试写回两份SourceHanSansHWSC-{Bold,Regular} SDF.asset：各约3500行字形/图集变化。运行前这两文件无改动，当前未还原、未提交，待用户确认处置；不能把缓存写回当功能修改。
- 尚未实现：P1至P6全部；尚未进行本轮Play/真机验收，不将文档计划当交付。

---

> 下方收工快照是旧版历史；当前进度以顶部本次会话为准。

## 0. 收工快照 / 恢复手册（2026-10-03 收盘）

### 一句话状态
阶段 25（TMP 全迁 + 数值表现强化）与阶段 25.1（缺字形警告修复）已完成并提交；工作区干净；Unity 全量 EditMode **75/75 通过**。下一步是 **Play 目视**，然后打包真机复验。

### 交付物与提交（本地 `main`）
| 提交 | 内容 |
|------|------|
| 6dd5df2 | TMP 迁移 + 数值表现强化 + Core 结算事件日志（当时 74/74 绿） |
| e023169 | 文档：记录 6dd5df2 + 下一件 |
| 0f8fa45 | 修 TMP 缺字形警告（✕ 无字形 + 图集 1024→2048 + 多图集）（75/75 绿） |
- `git status` 干净；远端 push 由用户本地执行（沙箱无法访问 github.com）。

### 恢复步骤（新开窗口照做）
1. 读 `progress.md`（本手册 + 阶段 25 / 25.1）+ `findings.md` 的「TMP 迁移与数值表现（阶段 25）」节。
2. 打开 Unity 工程 `G:\unity\CQ`（Unity 2022.3.62f2）。`Library/` 已在，首次打开会重编脚本；`Temp/` 不存在属正常（Unity 启动自建）。
3. **Play 目视验收清单**：
   - 血条加粗 + 条上 `HP/MAX` + 25/50/75% 刻度 + 残血脉冲；
   - 飘字（按伤害量放大、弹入、约 1.4s）+ 受击闪白/抖动；
   - 左栏「战报」逐条「谁〔技能〕 → 打谁 -N」；
   - 中文显示正常（思源黑体 SDF）；
   - Console **无** `The character with Unicode value ... was not found` 警告；
   - 3 关流程可打通，「下一关」按钮正常。
4. 通过后：打包 APK → 真机/MuMu 复验横屏 + 触控 + 返回键 → T13 录屏 → T14 验收清单。

### 跑测试的标准命令（PowerShell）
```powershell
& "G:\unity\Unity_2022.3.62f2\Editor\Unity.exe" -batchmode -nographics -projectPath "G:\unity\CQ" `
  -runTests -testPlatform EditMode -testResults "G:\unity\CQ\Logs\editmode.xml" `
  -logFile "G:\unity\CQ\Logs\batchmode.log"
```
- **不要加 `-quit`**：加了会在导入完成后立刻退出，一条测试都不跑、也不产出结果 XML。
- 结果/日志**不要写进 `Temp/`**：Unity 启动时会删掉工程 `Temp/` 目录。
- 受限沙箱下 Unity 因许可证 IPC（命名管道）不可达而返回码 **199**；需放宽沙箱后再跑（本会话用 danger-full-access 跑通）。
- 跑完（含 Play）后，两份 `Assets/Resources/Fonts/*SDF.asset` 会被写回字形缓存（各涨到 ~2MB，且图集像素其实没写进去），**必须还原**：
  `git checkout -- "Assets/Resources/Fonts/SourceHanSansHWSC-Regular SDF.asset" "Assets/Resources/Fonts/SourceHanSansHWSC-Bold SDF.asset"`
  本手册所述「图集 2048 + 多图集」改动已在 `0f8fa45` 提交，所以 checkout 会还原成「正确配置 + 空图集」，不会丢东西。
- `Logs/`（已 gitignore）留有本会话证据：`editmode-atlas.xml`（75/75 全绿）、`editmode-final.xml`（74/74）、`batchmode-*.log`；临时诊断脚本 `cmap_gid.py`（解析 `.otf` cmap 报告码点→字形 id）、`char_budget.py`（统计工程可显示字符数）、`memos_inspect.py`（只读查看 memos 库）。

### memos 记忆插件（本次启用）
- 状态：**已启用**。改动文件 `C:\Users\19793\.dsh\profiles\web\cordis.patch.yml` —— 删除了原先的 loader patch 条目 `- id: memos-local-memory` / `disabled: true`；插件 bundle `@memtensor/memos-local-plugin@2.0.20` 自带默认行（`enabled / recallEnabled / captureEnabled / toolsEnabled` 全 true）。`patchReload: live`，改完即生效。
- 运行态证据：viewer 端口 `127.0.0.1:18801` 可连（connect=True，且 patch 保存后新起了 node 进程）；`api_logs` 随本次 `memos_search` 增长（890 → 896）；成功召回 9/23–9/25 的历史 trace。
- 数据：`C:\Users\19793\.dsh\memos-plugin\data\memos.db`（2.5MB，未改动内容）。
- 已知遗留：`traces` 仍 32 条、最新时间戳 2026-09-30 —— 插件行被 disable 期间**今天（10/3）的对话没有入库**；启用后新回合/新会话应恢复记录，下次开机可用 `memos_search` 验证。另 `embedding_retry_queue` 有 32 条 pending、`api_logs` 有 `system_error` / `system_model_status`：本地嵌入模型没跑起来，语义召回退化为 FTS 关键词召回（能召回，排序偏弱）。要修就按插件 README 查模型下载/网络。
- 关闭方式（如需）：在同一个 patch 文件末尾加回
  ```yaml
  - id: memos-local-memory
    disabled: true
  ```
- 提示：**`progress.md` 才是可靠记录**，memos 只作辅助召回。

### 环境速查
- Unity：`G:\unity\Unity_2022.3.62f2`（AndroidPlayer 内置 SDK/NDK/JDK11；构建须走内置 JDK11，当前 `JAVA_HOME=G:\jdk21`）。
- Android：包名 `com.dreamworld233.cq`、横屏左、ARMv7+ARM64、minSdk 22、IL2CPP（菜单 `CQ/配置 Android PlayerSettings`）。
- 已有产物：`APK/CQ_v0.1.1.apk`（45MB，2026-09-23；TMP/数值改动后需重打）。
- 诊断日志开关：`BattleController.verboseClick`、`RadialSkillMenu.verbose`（当前仍为 true，确认没问题后可关）。

## 会话：规划阶段

### 阶段 1：读取需求与环境
- **状态：** complete
- 执行的操作：
  - 读取 `demo任务清单.md` + `战斗设计.md`。
  - 盘点环境（Unity 2022.3.62f2、URP 14.0.12、git 2.52.0、gh 未装、本地非 git 仓库）。
  - 按用户反馈两轮修订：AI 降为提效待办；补能量初值 / 前后排框架 / 行动条左侧 / LAN 不做；数值 JSON 驱动。
- 创建/修改的文件：
  - `task_plan.md`（详细开发计划，已重写）
  - `findings.md`（发现与决策，已更新）
  - `progress.md`（本文件）

### 阶段 2：GitHub 仓库重置
- **状态：** complete（用户本地完成，master 已删，留 main）
- 执行的操作：
  - `git ls-remote` 失败：`schannel: AcquireCredentialsHandle failed: SEC_E_NO_CREDENTIALS`。
  - `web_fetch github.com` 失败：`URL hostname "github.com" resolves to a non-public IP address`。
  - 结论：当前沙箱环境无法访问 github.com，重置需用户在本地终端执行。
  - 已写入 `.gitignore`（Unity 标准忽略）。
- 创建/修改的文件：
  - `.gitignore`

### 阶段 3：任务清单拆解
- **状态：** complete
- 执行的操作：
  - 拆第 1 周骨架为 T1–T7 具体任务（依赖图 + 契约 + 验收 + 委派）。
- 创建/修改的文件：
  - `tasks.md`

### 阶段 4：实现（T1）
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - 建 4 个 asmdef：`CQ.Core`（`noEngineReferences:true` 零引擎引用）、`CQ.Runtime`、`CQ.Editor`、`CQ.Tests.EditMode`。
  - Core 纯 DTO：6 类配置 + `LoadResult` + `ConfigValidator`（row/aggroWeight 到位）。
  - Runtime `ConfigJson`/`ConfigLoader`（JsonUtility + 校验 + 日志）；`Editor/ConfigInspectMenu`（菜单打印计数）；EditMode `ConfigLoaderTests`。
  - 20 个样例 JSON（3 角色 + 6 卡定义合 8 张 + 3 敌 + 5 状态 + 3 关）。
- 创建/修改的文件：
  - `Assets/Scripts/Core/**`（asmdef + 7 cs）
  - `Assets/Scripts/Runtime/Config/ConfigJson.cs`、`ConfigLoader.cs`（+ asmdef）
  - `Assets/Scripts/Editor/ConfigInspectMenu.cs`（+ asmdef）
  - `Assets/Tests/EditMode/ConfigLoaderTests.cs`（+ asmdef）
  - `Assets/StreamingAssets/Data/**`（20 JSON）
- 待办：Unity 打开工程编译 + Test Runner 跑 EditMode；确认无引擎编译错。
- 验证结果：Unity 编译通过（修复 CS0411 泛型推断，`LoadAll<T>` 加显式类型参数）+ EditMode 测试无报错全过（用户确认）。

### 阶段 5：实现（T2）
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - `Core/Combat/Unit.cs` + `Team.cs`（冻结契约 Unit 提前落地，字段/方法齐全）。
  - `Core/Mathd.cs`（`FloorToInt` 去尾统一入口）。
  - `Core/Battle/Rng.cs`（xorshift64* 可注入 seed）、`SpeedSorter.cs`（有效速度 + 同速我方先 + 稳定序）、`ActionQueue.cs`、`TurnManager.cs`（回合状态机骨架 + 胜负）。
  - EditMode `SpeedSorterTests.cs` + `TurnManagerTests.cs`。
- 创建/修改的文件：
  - `Assets/Scripts/Core/Combat/{Team,Unit}.cs`
  - `Assets/Scripts/Core/Mathd.cs`
  - `Assets/Scripts/Core/Battle/{Rng,SpeedSorter,ActionQueue,TurnManager}.cs`
  - `Assets/Tests/EditMode/{SpeedSorterTests,TurnManagerTests}.cs`

### 阶段 6：实现（T3–T6，4 subagent 并行）
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - 冻结共享契约 spine：`StatusType/StatusEffect/CombatContext/CombatContracts/Damage`（string id、Haste 瞬态）；契约 int→string 修正。
  - T3 `SkillResolver`（9 招+能量/大招）；T4 `IntentModel/TargetSelector/EnemyActionResolver`；T5 `CardDef/CardEffectResolver`；T6 `StatusResolver`。
  - 各附 EditMode 测试（SkillResolverTests 12、EnemyActionResolverTests 6、TargetSelectorTests 3、IntentModelTests 1、CardEffectResolverTests 6、StatusResolverTests 5）。
- 创建/修改的文件：
  - `Assets/Scripts/Core/Combat/{StatusType,StatusEffect,CombatContext,CombatContracts,Damage}.cs`
  - `Assets/Scripts/Core/Combat/{SkillResolver,IntentModel,TargetSelector,EnemyActionResolver}.cs`
  - `Assets/Scripts/Core/Cards/{CardDef,CardEffectResolver}.cs`
  - `Assets/Scripts/Core/Status/StatusResolver.cs`
  - `Assets/Tests/EditMode/*Tests.cs`（6 个新测试文件）
- 裁决：T4 的 `interruptible` 绑定「蓄力时心态」，打断仅当 interruptible 且 HasStatus(Interrupt)；接受。T5 `SourceUnitId` 赋值保留。
- 遗留：T5.1 抽牌/手牌管理移入 T7；interrupt 跳过回合时 `_pending` 蓄力是否取消，留 T7 集成定。

### 阶段 7：实现（T7 串联）
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - `Core/Cards/Deck.cs`：抽牌/存牌/打出 + 弃牌回洗，全走 Rng。
  - `Core/Battle/BattleEngine.cs`：确定性编排器（StartBattle → 输入 Phase 打牌/选招 → EndRound 速度混排+逐个结算+死亡+胜负+回合末衰减）。
  - `EnemyActionResolver.ResolveSkipped`（打断只走蓄力释放路径，`interruptible` 蓄力取消）；`SkillResolver.CanUse`（能量/大招门槛）。
  - `Runtime/BattleController.cs`（IMGUI 最小可玩：选招/打牌/结束回合/重开）+ `Editor/BattleSceneMenu.cs`。
  - `BattleEngineTests.cs` 6 测试（自动普攻胜 / 打断跳过并取消蓄力 / 能量门槛 / 抽牌封顶 / 敌方胜 / 同 seed 同态）。
- 创建/修改的文件：
  - `Assets/Scripts/Core/Cards/Deck.cs`
  - `Assets/Scripts/Core/Battle/BattleEngine.cs`
  - `Assets/Scripts/Core/Combat/EnemyActionResolver.cs`（+ResolveSkipped）
  - `Assets/Scripts/Core/Combat/SkillResolver.cs`（+CanUse）
  - `Assets/Scripts/Runtime/BattleController.cs`
  - `Assets/Scripts/Editor/BattleSceneMenu.cs`
  - `Assets/Tests/EditMode/BattleEngineTests.cs`
  - `tasks.md`、`progress.md`、`findings.md`
- 待办：Unity 编译 + 跑全量 EditMode；菜单 `CQ/创建战斗测试场景` 进最小可玩关。
- 验证结果：编译 + 全量 EditMode 全绿；level1 可过（用户确认）。

### 阶段 8：实现（T8 数值配平 + 3 敌模板 + 3 关编排）
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - 杂兵 `baseSpeed 4→7`：使 level1 出手顺序敌我混排，落实「速度排序」教学点。
  - `IntentConfig.buffDuration` + `EnemyActionResolver` 攻增不再硬编码 `Duration=1`；`ConfigValidator` 补 attackUp 校验；杂兵 `attackUp` 设 `buffDuration:2`（跨到下一击）。
  - `BalanceTests.cs` 4 测试（level1 纯普攻可过 / 混排 / 领主蓄力可打断 / 关卡引用无缺失）。
- 创建/修改的文件：
  - `Assets/Scripts/Core/Config/EnemyConfig.cs`（+buffDuration）
  - `Assets/Scripts/Core/Config/ConfigValidator.cs`
  - `Assets/Scripts/Core/Combat/EnemyActionResolver.cs`
  - `Assets/StreamingAssets/Data/Enemies/grunt.json`
  - `Assets/Tests/EditMode/BalanceTests.cs`
  - `tasks.md`、`progress.md`、`findings.md`
- 待办：Unity 编译 + 跑全量 EditMode（现 57 测试）。

### 阶段 9：实现（T9 意图完整结算 + 存牌爆发点验证）
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - `IntentFlowTests.cs` 3 测试：AOE 意图打全体存活玩家；打断后游标不前进、下回合重新亮同意图；跨回合存牌 + 同回合多出（爆发）。
  - 核对 `战斗设计.md` 第八节打断边界，与 BattleEngine 打断路径一致。
- 创建/修改的文件：
  - `Assets/Tests/EditMode/IntentFlowTests.cs`
  - `tasks.md`、`progress.md`、`findings.md`
- 待办：Unity 编译 + 跑全量 EditMode（现 60 测试）。

### 阶段 10：实现（T10 2.5D 表现）
- **状态：** complete（文件层；待用户 Unity 编译 + 目视验证）
- 执行的操作：
  - `Runtime/PositioningView.cs` 前后排站位（玩家右/敌左，前排低近/后排高远，同侧槽位横排）。
  - `Runtime/UnitView.cs` 世界单位占位（SpriteRenderer 色块 + TextMesh 名字/血量/意图）。
  - `BattleController.cs` 重写：世界单位生成 + 左侧常驻行动条 + 手牌/技能/意图 HUD + 自动建正交相机。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/PositioningView.cs`
  - `Assets/Scripts/Runtime/UnitView.cs`
  - `Assets/Scripts/Runtime/BattleController.cs`
- 待办：`CQ/创建战斗测试场景` → Play 目视（站位/行动条/意图/手牌）。

### 阶段 12（部分）：T12 确定性复验
- **状态：** 部分完成（test 待 Unity 跑）
- 执行的操作：`DeterminismTests.cs`（2 角色 + 领主 + 爪牙 + 打断/虚弱卡，逐回合同 seed 快照一致）。
- 创建/修改的文件：`Assets/Tests/EditMode/DeterminismTests.cs`

### 阶段 13（部分）：T13 面试讲稿 + 架构图
- **状态：** 部分完成（doc 已写；录屏待用户）
- 执行的操作：`交付/面试讲稿.md`（一句话可讲性 + 30s 口播 + 架构三件套 + mermaid + 2-3 分钟分镜 + 零硬编码抽查点）。
- 创建/修改的文件：`交付/面试讲稿.md`

### 阶段 14（部分）：T14 缓冲 + 验收核对
- **状态：** 部分完成（清单已写；目视/playtest/录屏待用户）
- 执行的操作：
  - `交付/验收清单.md`（6 条验收逐条状态 + 证据 + 待补）。
  - `BattleController` 串 3 关顺序连打（`_levels` 缓存 + `_levelIndex` + 「下一关」按钮 + 「已打穿全部关卡」）。
  - 零硬编码审计写入 `findings.md`（规则常量 vs 平衡数据分类）。
- 创建/修改的文件：`交付/验收清单.md`、`Assets/Scripts/Runtime/BattleController.cs`、`findings.md`

### 阶段 15：平台决策（Android 横屏）+ 修 2 bug
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - 平台定 Android 横屏为基准，PC 留录屏/验收底盘（测试仍 PC 跑）。
  - 查验 Android 环境：Unity 2022.3.62f2 AndroidPlayer 内置 SDK/NDK/JDK 全齐；外部 AndroidSdk 缺 ndk；`JAVA_HOME=JDK21` 需切内置 JDK11。Build Settings 切 Android 无报错（用户确认）。
  - BUG-A 修：`BattleEngine` 加 `CurrentOrder`（按需速度混排），`DrawActionBar` 改读；加回归测试 `CurrentOrder_Available_DuringInput`。
  - BUG-B 修：`DrawPlayers` 点我方删 `_selectedTarget = p.Id;`，目标独立保留。
- 创建/修改的文件：
  - `Assets/Scripts/Core/Battle/BattleEngine.cs`
  - `Assets/Scripts/Runtime/BattleController.cs`
  - `Assets/Tests/EditMode/BalanceTests.cs`
  - `task_plan.md`、`findings.md`、`progress.md`
- 验证结果：编译全绿 + 全量 EditMode 通过 + Play 目视「行动条即时刷 + 多角色选招掉血正常」均确认（用户确认，2 bug 已消）。

### 阶段 16：意图三分类 + 治愈选友方 + 减益意图
- **状态：** complete（文件层；待 Unity 编译 + 跑 NUnit 验证）
- 执行的操作：
  - 敌方意图三分类显示：攻击(谁+伤害)/自增益↑/给我方减益↓（`BattleController.IntentText`）。
  - 单体攻击/连击/蓄力/减益亮意图时预锁目标（`BattleEngine.LockIntentTargets` + `GetIntentTarget`；`EnemyActionResolver` 用锁定目标，已死回退）。
  - 新增敌方减益意图 `debuff`（`IntentConfig` 加字段 + `ConfigValidator` 校验 + resolver `Debuff`）；`grunt` 序列 `[basic, attackUp, debuff]`。
  - 治愈选友方：`guard`「守护」heal.target `self`→`single`；`ChooseSkill` 加目标合法性；`BattleController` 加友方目标按钮区 + 治愈默认自己。
- 创建/修改的文件：
  - `Assets/Scripts/Core/Config/{EnemyConfig,ConfigValidator}.cs`
  - `Assets/Scripts/Core/Combat/EnemyActionResolver.cs`
  - `Assets/Scripts/Core/Battle/BattleEngine.cs`
  - `Assets/Scripts/Runtime/BattleController.cs`
  - `Assets/StreamingAssets/Data/Enemies/grunt.json`、`Data/Characters/guard.json`
  - `Assets/Tests/EditMode/{EnemyActionResolverTests,BattleEngineTests}.cs`（+3 测试）
  - `findings.md`、`progress.md`
- 待办：Unity 编译 + 跑全量 EditMode（现 66 测试）；Play 目视三类意图 + 治疗指向友方。
- 验证结果：编译全绿 + 全量测试通过 + Play 目视三类意图/治疗/友方目标均确认（用户确认）。
- 补修：死亡敌人不再显示意图（`RefreshWorld` 与 `DrawEnemies` 按 `IsDead` 隐藏）。

### 阶段 17：Android 横屏适配（Part 1 代码层）
- **状态：** complete（文件层；待 Unity 编译 + 用户跑菜单配 PlayerSettings + Build）
- 执行的操作：
  - `BattleController.Start` 锁 `Screen.orientation = LandscapeLeft`；`Update` 读 `KeyCode.Escape` → `Application.Quit()`（Android 返回键）。
  - 新增 `Editor/AndroidBuildMenu.cs`：菜单 `CQ/配置 Android PlayerSettings` 设包名 `com.dreamworld233.cq` + 横屏左 + ARMv7/ARM64 + minSdk 22 + IL2CPP。
  - 结论：`CanvasScaler` 不适用（全 IMGUI，无 Canvas）；`OnApplicationPause/Focus` 无需代码（确定性状态机，内存态自然保留）。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/BattleController.cs`
  - `Assets/Scripts/Editor/AndroidBuildMenu.cs`
  - `findings.md`、`progress.md`
- 待办：Unity 编译 + 跑菜单 `CQ/配置 Android PlayerSettings` → `CQ/创建战斗测试场景`（若场景无 BattleController）→ 保存场景 → Build APK → MuMuPlayer 或真机安装验证横屏 + 触控 + 返回键。
- 验证结果：编译无误 + 全量测试全绿 + Build APK 成功（用户确认）。**真机蓝屏**：仅相机背景、无单位、无 HUD、无可交互 → 见阶段 18。

### 阶段 18：真机空屏诊断记录（未修）
- **状态：** 已记录根因与修复方向，待新对话修
- 现象：Android 真机只蓝屏；编辑器 Play 正常。
- 候选根因 1（强证据）：`ConfigJson` 用 System.IO（`Directory.Exists`/`File.ReadAllText`）读 `Application.streamingAssetsPath`，Android 该路径在 APK 内（jar:）读不到 → 配置失败 → `NewBattle` `return`，不 `SpawnWorld` → 无单位。
- 候选根因 2：`CQ/创建战斗测试场景` 只在内存 `AddComponent`，不 `MarkSceneDirty`/不保存 → Build 的 SampleScene 磁盘版可能无 BattleController。
- 修复方向：见 `findings.md`「真机空屏 bug」节（Android 安全读 JSON：UnityWebRequest / Resources / 拷 persistentDataPath；`[RuntimeInitializeOnLoadMethod]` 引导建 BattleController）。
- 涉及文件（待改）：`Assets/Scripts/Runtime/Config/ConfigJson.cs`、`ConfigLoader.cs`、`Editor/BattleSceneMenu.cs`、或新增 bootstrap。

### 阶段 19：修真机蓝屏（JSON 迁 Resources + 控制器引导）
- **状态：** complete（用户真机验证通过）
- 执行的操作：JSON 迁 `Assets/Resources/Data`；`ConfigJson.LoadAllFromResources`（`Resources.LoadAll<TextAsset>`）；`BattleController` 加 `[RuntimeInitializeOnLoadMethod]` 兜底。
- 验证结果：真机不再蓝屏，有世界单位 + HUD（用户确认「现在可以了」）。

### 阶段 20：uGUI 交互重做（点选 → 弧形技能 → 索敌）
- **状态：** complete（文件层；待 Unity 编译 + 目视验证）
- 执行的操作：
  - 删 OnGUI 表现层，全迁 uGUI（Canvas Screen Space Overlay + CanvasScaler 1920×1080 + EventSystem + StandaloneInputModule）。
  - 世界单位加 `BoxCollider2D`，`Physics2D.OverlapPoint` 点选：点我方角色选中（黄环）→ 角色上方弧形弹出 3 技能按钮 → 点敌人/友方索敌出招。
  - 单体伤害点敌、单体治疗点友方、自愈/全体立即出招；不可用技能置灰。
  - 左侧行动条（回合+出手顺序+最近事件）、底部大按钮 结束回合/重开/下一关。
  - 手牌本期隐藏（卡牌交互后置）。
  - 内置 CJK 字体 `Assets/Resources/Fonts/SimHei.ttf`（临时，正式发布换开源 Noto Sans SC）。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/BattleController.cs`（重写表现层）
  - `Assets/Scripts/Runtime/BattleUi.cs`（新）
  - `Assets/Scripts/Runtime/RadialSkillMenu.cs`（新）
  - `Assets/Scripts/Runtime/UiKit.cs`（新）
  - `Assets/Scripts/Runtime/UnitView.cs`（+BoxCollider2D + 高亮环）
  - `Assets/Resources/Fonts/SimHei.ttf`（+ 元数据由 Unity 生成）
  - `findings.md`、`progress.md`
- 待办：Unity 编译通过（用户确认无报错）→ **点击无反馈 + 无环形技能**（见 findings「点击无反馈 bug」）→ 下午打日志定位再修。
- 验证结果：编译无报错；世界单位 + HUD 可见，但点角色无高亮、无弧形菜单（待修，下午）。

### 阶段 21：UI 素材盘点 + 交互方向修订（记录，明早动手）
- **状态：** 记录完成（代码未动）
- 素材到位（`Assets/Resources/` 下）：
  - TMP 字体：`Fonts/SourceHanSansHWSC-Regular SDF.asset` + `Bold SDF.asset`（Font Asset Creator 生成，替代 legacy `Font` + SimHei）。
  - 示意图 5 张：`Arts/UI/{战士,法师,盾兵,小怪,精英怪}示意图.png`（对应 sword/mage/guard + grunt/claw）。**缺 lord 一张**，可暂复用精英怪。
  - 意图图标 4：`Arts/UI/{攻击,防御,增益,减益}.png`；选中框 `按钮选中框.png`。
  - 缺：技能图标 9 个（后置）、面板/按钮 9-slice 描边底（后做）、lord 示意图。
- 轮盘改法（用户定）：放弃扇形按钮，改 **3 个圆形可交互按钮**，代码定位在「选中角色上方」沿弧分布；技能图标文字占位，后补。
- 明早顺序：
  1. 字体迁 TMP：`UiKit.Font` 加载 `TMP_FontAsset`；`BattleUi`/`RadialSkillMenu` 文本 `Text`→`TextMeshProUGUI`。
  2. 修「点击无反馈」bug（打日志二分，见 findings）——轮盘显示依赖此项。
  3. `RadialSkillMenu` 改 3 圆按钮 + 锚点升到角色头顶弧状。
  4. 意图图标 + 选中框接入 UI。
  5. 单位 sprite 换 5 张示意图（替纯色方块）。

### 阶段 22：表现层接线 + 点选链路修复（代码层）
- **状态：** 文件层完成；**待 Unity 编译 + Play 验证**
- 执行的操作：
  - `EnsureCamera` 强制正交（size 5）+ 归位 (0,0,-10) + 深色底。理由：场景里已有 `SampleScene` 的 MainCamera，若是透视相机则 `ScreenToWorldPoint` 落点不在世界 z=0 平面 → 点选必打空（候选根因）。
  - 点选改稳健：`Physics2D.OverlapPointAll(point, Physics2D.AllLayers)`（默认 layerMask 排除 Ignore Raycast 层会静默打空）+ 落空时 `OverlapCircleAll` 半径 0.9 触控容错取最近单位。
  - 加诊断日志：`[CQ.Cam]` / `[CQ.Click]`（screen/world/overUi/hit/unit）/ `[CQ.Radial]`（canvas 尺寸/世界锚点/局部锚点/每个按钮落点）。开关 `verboseClick` / `verbose`。
  - 新增 `ArtLibrary.cs`：按约定名从 `Resources/Arts/UI/` 加载示意图与意图图标，缺图回退纯色；敌人实例 id `grunt_0` → 配置 id `grunt`。映射 sword/mage/guard/grunt/claw → 战士/法师/盾兵/小怪/精英怪（lord 暂复用精英怪）。
  - `UnitView` 重做：示意图子物体（按高 1.8 世界单位缩放）+ 死亡转灰 + 名字/血量/能量文字移到角色下方 + 文字设 `UiKit.Font`（CJK；原 Arial 无中文字形）+ `MeshRenderer.sortingOrder = 50`（原先文字被 sprite 盖住）+ 敌人意图图标 sprite + 选中高亮改「四边方框」（原先是一个方块整块盖住角色，看不出是选中）。
  - `RadialSkillMenu` 重做：改 3 个圆形按钮（纯代码生成圆图），锚点从角色中心上移 105 + 弧半径 195，弧 140°→40°，直径 190（移动端热区）。
  - 开局即亮当前角色技能弧（`NewBattle` 末尾 `ShowRadial`）。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/ArtLibrary.cs`（新）
  - `Assets/Scripts/Runtime/{BattleController,UnitView,RadialSkillMenu,UiKit}.cs`
  - `findings.md`、`progress.md`
- 阻塞项：**TMP 迁移未做** —— 工程内无 `Assets/TextMesh Pro/`（TMP Essential Resources 未导入，全库只有 PackageCache 里的样例）。未导入时 TMP 组件运行时报找不到 TMP Settings，文字不显示。需用户执行 `Window > TextMeshPro > Import TMP Essential Resources` 后再迁 `TextMeshProUGUI`。
- 待办：Unity 编译 → Play 看 `[CQ.Cam]` / `[CQ.Click]` / `[CQ.Radial]` 日志定位剩余问题。
- 用户实测反馈：TMP 已导入 ✓；5 张示意图 + 4 图标 Import Settings 均为 `Sprite (2D and UI)` ✓；发现 3 个问题（怪物过挤选不中、友方碰撞体重叠选不中法师、点空白自动推进回合）。

### 阶段 23：定位真根因（锚点反向 → 隐形全屏按钮）+ 阵型与选中修复
- **状态：** 文件层完成；**待 Unity 编译 + Play 复验**
- **真根因**：`BattleUi.BuildBottomControls` 三处写成 `Rect(rt, 1f, 1f, 0f, 0f, ...)`，而 `Rect` 的第 3/4 参数是 `anchorMax` → `anchorMin=(1,1)`、`anchorMax=(0,0)` **锚点反向**，静默变成拉伸矩形，实际占 x∈[−36,1584]、y∈[36,986]（1920×1080）→ 近全屏。
  - 后果 1：`PointerOverUi()` 恒 true → `HandleUnitClick` 每次提前 return → 点谁都无反馈（阶段 20 的 bug 真因）。
  - 后果 2：该按钮被点中 → `OnEndRound` → 点空白推进回合。
  - 技能圆钮仍可用，因为它们是 Canvas 更靠后的子节点，射线在上层。
- 修法：
  - 三处锚点成对：结束回合/重开 → `Rect(rt, 1f, 0f, 1f, 0f, ...)`（右下）；下一关 → `0.5f,0.5f,0.5f,0.5f`（居中）。
  - 命中改取「最近」：`PickUnitCollider(hits, point)` 按距离决胜（原先取数组第一个 → 某个角色永远选不中）。
  - 阵型不再缩放根节点（`SpawnUnit` 去掉 `localScale 1.3`，缩放会放大碰撞体）；间距参数化 `BattleLayout.teamGap = 2.0` + `arenaShift = 0.9`；碰撞体 `1.4 × 1.7` → 同侧间距 > 碰撞体宽，互不重叠。
  - 左侧行动条：宽 400 → 340，`raycastTarget = false`（纯展示不挡点选）。
  - `BattleLayout` 挂 `BattleController` 作 `[SerializeField]`，Play 中可在 Inspector 实时调间距/位移/前后排 y。
  - `EndRoundAction` 加 `[CQ.Round]` 日志（点开看堆栈确认触发者）。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/{BattleUi,PositioningView,BattleController,UnitView}.cs`
  - `findings.md`、`progress.md`
- 待办：Unity 编译 → Play 复验「点每个单位都能选中（含法师）→ 头顶弧 3 圆钮 → 点目标索敌」；确认 `[CQ.Click] overUi=False` 且点空白不再推进回合。
- 说明：用户提议「手工搭场景解决 UI 问题」——不需要。全部 UI 由代码构建，问题在代码常量与锚点，场景不参与。
- 用户复验：位置分布 OK。新报 3 个问题（守护选友方被顶掉、看不到数值/血条、领主关打不过）。

### 阶段 24：指向反馈 + 数值可见 + 难度调整
- **状态：** complete（用户确认「流程能跑通」）
- 问题 1「守护点友方被顶掉」根因：`OnUnitTapped` 里 `Team.Player` 分支无条件 `_selectedActor = unit.Id; _pendingSkill = null; ShowRadial()` → 待选治疗目标时点友方被当成「切换选中角色」。
  - 修法：该分支前置判断 `_pendingSkill != null && NeedsSingleAlly(_pendingSkill)` → `TryChooseSkill(_selectedActor, _pendingSkill.id, unit.Id)` 后 `return`（不再顶掉选中角色）。
  - 反馈加强：`TryChooseSkill` 成功时日志补「（已登记，结束回合结算）」；左栏行动条给已登记角色加〔技能名〕标记（`_queued` 字典，`RefreshHud` 渲染，`EndRound`/`NewBattle` 清空）。
- 问题 2「看不到数值」：`UnitView` 加纯代码数值条 —— 血条（双方，宽 1.5，比例填充、左对齐、≤30% 转红）+ 能量条（青）+ 大招条（金，仅我方）；名字/数值文字（`HP/MAX`、`能 x/3  大 y/3`）下移到条下方。新增 `FloatingText`（世界空间飘字，上浮淡出 1.1s 自毁）+ `BattleController.FloatDeltas`（回合结算前后血量快照差 → 绿 `+N` / 红 `-N`），让「打没打中 / 治没治上」一眼可见。
- 问题 3「领主关打不过」：调 JSON（数值仍零硬编码）。
  - `lord.json`：`maxHp 300→200`、`basic 40→22`、`aoe 25→12`、`charge nextDamage 120→50`（保留 `interruptible: true`，`BalanceTests.Lord_Has_Interruptible_Charge` 仍通过）。
  - `claw.json`：`maxHp 90→60`、`slash 14→10`、`multi 6×3→4×3`。
  - 依据：关卡 3 = 领主 + 2 爪牙，原总血 480、敌方每回合有效伤害约 75，我方总血 340 + 治疗约 60 → 5 回合内必崩；改后总血 320、敌方每回合约 55，先杀爪牙可翻盘。打断卡（蓄力唯一解法）本期 UI 未接，故蓄力从 120 降到 50。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/FloatingText.cs`（新）
  - `Assets/Scripts/Runtime/{UnitView,BattleController}.cs`
  - `Assets/Resources/Data/Enemies/{lord,claw}.json`
  - `findings.md`、`progress.md`
- 待办：Unity 编译 → 跑全量 EditMode（应仍全绿）→ Play 复验 3 点。
- 验证结果：用户确认「流程能跑通」（选目标不再被顶掉、数值可见、领主关能过）。
- 用户新反馈（延后，不着急）：**血条与伤害数值还不够醒目**，要更明显的数值表现（更大字号/更长的血条/伤害数字更突出/掉血闪白等）。

### 阶段 25.1：修缺字形警告（✕ U+2715 + 图集容量）
- **状态：** complete（Unity 全量 EditMode 75/75 通过；日志内 TMP 缺字形警告计数 0）
- 用户实测反馈：Play 正常、击中与飘字效果满意；刷一条警告
  `The character with Unicode value \u2715 was not found in the [SourceHanSansHWSC-Bold SDF] font asset ... replaced by Unicode character \u25A1`。
- 定位（两条独立原因）：
  1. ✕(U+2715) 确实不在思源黑体 cmap（Python 解析 `.otf` 得 glyph 0）→ 技能按钮「不可用」标记改 ×(U+00D7)。
  2. 图集容量不足：资产图集 1024² + 未开多图集，pointSize 90/padding 9 下只装约 80 个 CJK 字形；demo 全部可显示文案统计有 **364 个不同汉字** → 用满后新汉字全变 □。诊断证据：新增 关(U+5173)/并(U+5E76) 失败，但已缓存的字返回 true，图集 `used102/free77` 无空位；同字体 cmap 里 U+5173 → glyph 10921（非 0），坐实是容量问题。
- 修法：两份 SDF 资产 `m_AtlasWidth/Height` 1024→2048、自由矩形 1023→2047、多图集 0→1；`TmpKit.EnsureUsable` 运行时兜底再开多图集；新增 `FontAssetTests.All_Demo_Text_Fits_In_Font_Assets`（配置全名称 + UI 文案灌入两份资产，断言零缺字）。
- 创建/修改的文件：
  - `Assets/Resources/Fonts/SourceHanSansHWSC-{Regular,Bold} SDF.asset`（仅 4 处数值，diff 共 10 行）
  - `Assets/Scripts/Runtime/TmpKit.cs`、`Assets/Scripts/Runtime/RadialSkillMenu.cs`
  - `Assets/Tests/EditMode/FontAssetTests.cs`
  - `findings.md`、`progress.md`
- 验证：`Unity.exe -batchmode -runTests -testPlatform EditMode` → **75/75 通过**，日志中 `was not found / Unable to add / atlas texture` 警告 0 条。
- 待办：Play 目视确认不再有该警告；其余不变。

### 阶段 25.2：素材接线收尾（选中圆环 / 技能图标占位 / 关诊断日志）
- **状态：** complete（Unity 全量 EditMode 78/78 通过；缺字形警告 0）
- 执行的操作：
  - 探明素材：`按钮选中框.png` 用 PIL 实测是**空心白色圆环**（200×200，环宽约 11px、外径约 184px、近白），不是矩形 9-slice 底框 —— 不做面板拉伸，改当「脚下选中圆环」用。
  - `UnitView`：选中(黄)/可作目标(青)时脚下显圆环（`RingY=-0.78`、直径 1.9），原代码四边方框留作素材缺失兜底；`SetHighlight` 圆环优先。
  - 技能图标占位：`ArtLibrary.SkillIcon`（伤害→攻击图标、治疗→增益图标）；`UiKit.MakeCircleButton` 加 `icon` 参数（图标居上、文字下沉）。真图标 9 个到位只改 `SkillIcon` 一个映射。
  - 关诊断日志：`BattleController.verboseClick`、`RadialSkillMenu.verbose` 默认 false。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/{ArtLibrary,UiKit,UnitView,RadialSkillMenu,BattleController}.cs`
  - `Assets/Tests/EditMode/ArtLibraryTests.cs`（新，3 条）
  - `findings.md`、`progress.md`
- 验证：`dotnet build CQ.Runtime.csproj` 0 error；`Unity.exe -batchmode -runTests -testPlatform EditMode` → **78/78 通过**（`ArtLibraryTests 3/3`）。跑完已 `git checkout` 还原两份 SDF 字形缓存。
- 仍缺（素材层，用户后补）：lord 真示意图、9 个真技能图标、面板/按钮 9-slice 底。

### 阶段 25：TMP 迁移 + 数值表现强化（合并做）
- **状态：** 代码层完成；离线验证通过；**待 Unity 全量 EditMode + Play 目视**
- 执行的操作：
  - **TMP 迁移**（原第二件）：
    - 新增 `Runtime/TmpKit.cs`：字体资产装载 + **材质自愈**。工程内两份 SDF 资产（思源黑体 HW SC Regular/Bold）由 Font Asset Creator 生成，但 `material: {fileID: 0}`（空）→ TMP 3.0.7 在 `TextMeshPro.LoadFontAsset()` 里只打警告，随后对 null 材质赋值会抛异常、文字不显示。故按需补建 `TextMeshPro/Mobile/Distance Field` 材质（MainTex = 资产图集纹理，Dynamic 模式运行时逐字栅格化；`Texture2D.Resize` 原地改同一对象，材质引用不失效）。
    - `TmpKit.MakeWorldText`：世界空间文字统一入口（RectTransform + `TextMeshPro` 3D + 描边 + sortingOrder）+ `FitWorldHeight`（量测后按**目标世界高度**定标，每次从缩放 1 重置，改文字重调不漂移）。
    - `UiKit` 字体改 `TMP_FontAsset`（`Font` / `FontBold`）；`MakeText` 产出 `TextMeshProUGUI`（`TextAnchor`→`TextAlignmentOptions` 映射）。
    - `BattleUi` 文本字段全改 `TextMeshProUGUI`；`UnitView`/`FloatingText` 的 `TextMesh` 改 `TextMeshPro`（3D）。
    - asmdef：`CQ.Runtime` / `CQ.Tests.EditMode` 的 references 加 `Unity.TextMeshPro`。
    - 删 `Assets/Resources/Fonts/SimHei.ttf`（+meta，9.3MB，版权与体积都不合适）。
  - **数值表现强化**（原第一件）：
    - 血条加粗加长：`BarW 1.5→1.9`、底框高 `0.42`/填充 `0.30`（原 0.2/0.16），加外描边 + 25/50/75% 刻度；≤30% 血条转红并呼吸脉冲。
    - 血条上直接写 `HP/MAX`（粗体 + 黑描边），名字下移到条下，我方另起一行「能 x/3 大 y/3」。
    - 飘字（`FloatingText`）：TMP 粗体 + 描边，字号按伤害量放大（0.46~0.92 世界高），弹入缩放 0.6→1.18→1.0，时长 1.1→1.4s。
    - 受击反馈：`UnitView.Flash(delta)` → 掉血闪白 + 横向抖动 0.22s，回血闪绿。
    - 左栏新增「战报」：逐条「谁 用什么 → 打谁 -N」，点掉血最直白的证据。
  - **Core 新增结算事件日志**（战报数据源，`Core` 保持无文案）：
    - `Core/Battle/BattleEvent.cs`：`Kind`(Skill/Card/Intent) + `ActorId` + `ActionId` + `TargetId` + `HpDelta` + `TargetDead`。
    - `BattleEngine`：`RoundEvents`；`ActUnit` 结算前后血量快照差 → 每个变化单位一条（AOE = N 条），无变化记 1 条 `HpDelta=0`（增益/减益）。事件描述在结算**前**取（敌人意图游标结算后前进）。每回合 `EndRound` 开头清空，表现层用游标 `DrainEngineEvents` 播报，不重复。
- 创建/修改的文件：
  - `Assets/Scripts/Runtime/TmpKit.cs`（新）
  - `Assets/Scripts/Core/Battle/BattleEvent.cs`（新）
  - `Assets/Scripts/Core/Battle/BattleEngine.cs`
  - `Assets/Scripts/Runtime/{UiKit,BattleUi,UnitView,FloatingText,BattleController}.cs`
  - `Assets/Scripts/Runtime/CQ.Runtime.asmdef`、`Assets/Tests/EditMode/CQ.Tests.EditMode.asmdef`
  - `Assets/Tests/EditMode/BattleEventTests.cs`（新，4 测试）、`FontAssetTests.cs`（新，4 测试）
  - 删除 `Assets/Resources/Fonts/SimHei.ttf`（+meta）
  - `progress.md`、`findings.md`
- 离线验证（不依赖 Unity 的快速回路，Temp/CoreCheck 下自建，非交付物）：
  - `dotnet build CQ.Runtime.csproj` 0 error 0 warning（用 Unity 生成的 csproj + 手补 TMP 引用与新文件条目）。
  - 纯 Core 的 49 个 EditMode 测试全过（含新增 `BattleEventTests` 4 条）。说明：Unity 自带 `nunit.framework.dll` 是 net35、Assert 依赖 `CallContext`，在 .NET Core 下 `TypeLoadException`，故临时写了最小断言替身（见 `findings.md`）。
- **Unity 权威验证：全量 EditMode 74/74 通过**（`Unity.exe -batchmode -runTests -testPlatform EditMode`，从「与提交一致的工作区」起跑）：
  - `BalanceTests 6/6`、`BattleEngineTests 8/8`、`BattleEventTests 4/4`、`CardEffectResolverTests 6/6`、`ConfigLoaderTests 3/3`、`DamageTests 3/3`、`DeterminismTests 1/1`、`EnemyActionResolverTests 7/7`、`FontAssetTests 4/4`、`IntentFlowTests 3/3`、`IntentModelTests 1/1`、`SkillResolverTests 12/12`、`SpeedSorterTests 4/4`、`StatusResolverTests 5/5`、`TargetSelectorTests 3/3`、`TurnManagerTests 4/4`。
  - 期间修 1 个测试自身错误：`textBounds` 是**文字局部空间**（不受 transform 缩放影响），断言世界高度必须读渲染器 `bounds`。
- 待办：Play 目视（血条/条上数值/飘字/闪白/抖动/战报/中文字体/三关流程）。

## 测试结果
| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| 全量 EditMode（阶段 25 后，74 条） | `Unity.exe -batchmode -runTests -testPlatform EditMode` | 全绿 | total=74 passed=73 failed=1（`FontAssetTests` 世界高度断言写错，测试自身 bug） | 修测试后复跑 |
| 全量 EditMode（同上，修断言后） | 同上 | 全绿 | total=74 passed=74 failed=0 | 通过 |
| 全量 EditMode（阶段 25.1，加字符覆盖用例 75 条） | 同上 | 全绿 | total=75 passed=74 failed=1（`Ui_Symbols_...` 缺 U+5173 → 暴露图集容量不足） | 修图集后复跑 |
| 全量 EditMode（图集 2048 + 多图集） | 同上 | 全绿 + 0 缺字形警告 | total=75 passed=75 failed=0；日志 `was not found / Unable to add / atlas texture` 计数 0 | **通过** |
| 离线纯 Core 49 条（`Temp/CoreCheck`，非交付物） | `dotnet run`（自写 NUnit 断言替身） | 全过 | 通过 49 / 失败 0 | 通过（证据辅助） |
| `dotnet build CQ.Runtime.csproj` | Unity 生成的 csproj + 手补 TMP 引用 | 0 error | 0 error 0 warning | 通过 |

## 错误日志
| 时间戳 | 错误 | 尝试次数 | 解决方案 |
|--------|------|---------|---------|
| 规划 | git/github 不可达（schannel 无凭据；DNS 非公网 IP） | 2 | 沙箱无网络，改由用户本地执行 reset |
| 2026-10-03 | Unity batchmode 返回码 199：`Connection to channel LicenseClient-... refused` → 许可证客户端 IPC 不可达 | 2 | 受限沙箱的命名管道限制；放宽沙箱（danger-full-access）后正常 |
| 2026-10-03 | `-runTests` 同时带 `-quit`：Unity 导入完就退出，0 测试执行、无结果 XML | 1 | 去掉 `-quit`，测试跑完自行退出 |
| 2026-10-03 | TMP 缺字形警告 `Unicode value \u2715 was not found ... replaced by \u25A1` | 3 | 双根因：✕ 字形不存在（改 ×）+ 图集 1024 只装约 80 个 CJK 字形（改 2048 + 多图集）。见阶段 25.1 |
| 2026-10-03 | 测试用 `textBounds` 断言世界高度失败（读到 6.95 而非 0.3） | 1 | `textBounds` 是文字局部空间；世界高度须读渲染器 `bounds` |
| 2026-10-03 | 临时 `.NET` 离线跑 Core 测试：Unity 自带 `nunit.framework.dll`（net35）`Assert` 依赖 `CallContext` → `TypeLoadException` | 1 | 自写最小断言替身（`Temp/CoreCheck`，已被 Unity 清理，可重建） |

## 五问重启检查
| 问题 | 答案 |
|------|------|
| 我在哪里？ | 阶段 25 + 25.1 完成并提交（`0f8fa45`）：TMP 全迁（含字体材质自愈、图集 2048 + 多图集）+ 数值表现强化（血条/条上数值/飘字/闪白抖动/战报）；Unity 全量 EditMode **75/75 绿**。仅剩 Play 目视 |
| 我要去哪里？ | Play 目视（Console 无缺字形警告）→ 打包真机复验（横屏/触控/返回键）→ 剩余素材接线（选中框 9-slice、9 技能图标、lord 示意图）→ T13 录屏 → T14 验收 |
| 目标是什么？ | Android 横屏可玩可讲的 2.5D 回合制垂直切片 |
| 我学到了什么？ | 资产「看起来导入了」不等于可用（SDF 资产 material 为空 → 文字必不显示）；TMP `textBounds` 是局部空间，世界尺寸要读渲染器 bounds；Dynamic 字体资产会把字形缓存写回资产且像素未必真写入，别提交；字体图集容量会静默变成 □（1024² 只装约 80 个 CJK 字形，本项目文案有 364 个汉字）；引擎级事件日志放 Core 但**只给机器 id**，文案留表现层 |
| 我做了什么？ | 阶段 17 Android 适配 + 18 诊断 + 19 修真机蓝屏 + 20 uGUI 重做 + 21 素材盘点 + 22 表现层接线 + 23 定位真根因 + 24 指向/数值/难度（已验收）+ **25 TMP 迁移 + 数值表现强化** + **25.1 缺字形修复** |

## 下次继续（从这开始）
- **先看第 0 节「收工快照 / 恢复手册」**：里面有恢复步骤、Play 验收清单、跑测试命令与坑、memos 插件状态、环境速查。
- **新窗口恢复**：读 `progress.md` 阶段 25 / 25.1 + `findings.md`「TMP 迁移与数值表现（阶段 25）」节；再看 `Assets/Scripts/Runtime/` 下 `TmpKit.cs`（字体装载/定标/图集）/ `UiKit.cs` / `BattleUi.cs` / `UnitView.cs` / `FloatingText.cs` / `BattleController.cs`。
- **当前可用状态**：Play 直接跑（无需搭场景，`BattleController` 有 `[RuntimeInitializeOnLoadMethod]` 自举）。点我方角色 → 头顶弧 3 圆钮（带占位图标）→ 点敌/友方索敌 → 结束回合；选中的单位脚下显黄色圆环，可选目标显青色圆环。3 关可打。全量 EditMode 78/78 绿。
- **第一件**：Play 目视复核 —— 血条（加粗/刻度/条上 `HP/MAX`/残血脉冲）、飘字（更大更久/按伤害放大）、受击闪白 + 抖动、左栏「战报」逐条伤害、中文字体、脚下选中圆环、技能钮图标，且 Console 无缺字形警告。常量在 `UnitView`（顶部布局常量）与 `FloatingText`（`Life`/`Pop*`/`MinH`/`MaxH`）。
- **第二件（阶段 25.2 已做，只剩素材缺口）**：`按钮选中框.png` 已接成**脚下选中圆环**（实测是空心圆环，不是 9-slice 底框）；技能图标用「攻击/增益」占位（真图标 9 个到位只改 `ArtLibrary.SkillIcon`）；`verboseClick` / `verbose` 诊断日志已关。仍缺（素材层，用户后补）：lord 真示意图、9 个真技能图标、面板/按钮 9-slice 底。
- **第三件（后置）**：卡牌交互接回（uGUI 底部手牌面板）；`BattleEngine.RoundEvents` 已带 `Kind=Card` 分支与 `IntentLabel`，接回即用。接回后蓄力可被打断，领主关可再调硬（现 `lord.json` 蓄力 50 是为「无打断卡」而降的）。
- 之后：打包真机复验（横屏 + 触控 + 返回键）→ T13 录屏 → T14 验收。
- 提交：本地 `git commit` 由我执行；远端 push 用户本地完成。

## 提交记录（本次会话）
| 提交 | 内容 |
|------|------|
| 9875b46 | T1 脚手架 + 数据驱动 + EditMode 测试 |
| (T2) | T2 回合骨架 + 战斗共享契约 |
| (T3-T6) | 角色技能/敌人意图/目标选择/卡牌/状态（4 subagent） |
| 97e1331 | T7 串联闭环 + 最小可玩场景 |
| e7e925e | T8 数值配平 + 教学点测试 |
| ab6db4a | T9 意图完整结算 + 存牌爆发点验证 |
| 0cc23ad | 记录 T1-T9 全绿 + 下次继续指针 |
| dc17187 | T10 2.5D 表现 |
| e9d9d50 | T12 确定性复验测试 |
| 28ed332 | T13 面试讲稿 + 架构图 |
| d5cd586 | T14 验收清单 + 3 关连打 + 零硬编码审计 |
| e6e4f9b | 记录 T10-T14 推进 |
| 0b7b9cb | 3 关自动普攻终止保障测试 |
| 6de92b5 | 记录用户实测 2 bug + 修法 |
| 8271ccc | 意图三分类 + 治愈选友方 + 目标预锁 + 减益意图 + 修 2 bug |
| cdd014b | uGUI 交互重做 + 表现层接线 + 点选真根因修复（阶段 19–24）+ 字体/示意图/TMP 资源 |
| 6dd5df2 | TMP 全迁（材质自愈）+ 数值表现强化 + Core 结算事件日志 + 74/74 EditMode 全绿 |
| e023169 | 文档：记录 6dd5df2 + 下一件（Play 目视） |
| 0f8fa45 | 修 TMP 缺字形警告（✕ 无字形 + 图集 1024→2048 + 多图集）+ 字符覆盖回归 + 75/75 全绿 |
| （未提交，不属仓库） | `C:\Users\19793\.dsh\profiles\web\cordis.patch.yml` 启用 memos 插件；`Logs/` 下证据与临时脚本（gitignore） |
| (阶段 25.2) | 素材接线收尾：选中圆环 + 技能图标占位 + 关诊断日志 + ArtLibraryTests（78/78 绿） |

---

*每个阶段完成后或遇到错误时更新此文件。*