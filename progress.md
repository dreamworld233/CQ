# 进度日志

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

## 测试结果
| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| - | - | - | - | - |

## 错误日志
| 时间戳 | 错误 | 尝试次数 | 解决方案 |
|--------|------|---------|---------|
| 规划 | git/github 不可达（schannel 无凭据；DNS 非公网 IP） | 2 | 沙箱无网络，改由用户本地执行 reset |

## 五问重启检查
| 问题 | 答案 |
|------|------|
| 我在哪里？ | T1–T9 全绿；T10–T14 全部推进；62 个 EditMode 测试全绿（用户确认）；用户实测发现 2 个待修 bug（见下），已记录待新窗口修 |
| 我要去哪里？ | 修 2 个 bug（行动条当前顺序 / 点我方格设目标为自己导致伤害丢失）→ T11 收口 → T12 回归 → T13 录屏 |
| 目标是什么？ | 2 周可玩可讲的 2.5D 回合制垂直切片 |
| 我学到了什么？ | 见 findings.md（含「待修 bug」两节：BUG-A 行动条、BUG-B 自伤） |
| 我做了什么？ | 战斗闭环 + 2.5D 表现 + 3 关连打 + 确定性测试 + 讲稿/验收清单 |

## 下次继续（从这开始）
- 命令行恢复：读 `progress.md` 本表 + `findings.md`「待修 bug」节 + `tasks.md` 勾选。
- **优先修 2 个 bug**（详见 `findings.md` 待修 bug）：
  - BUG-A：行动条只显上回合顺序 → `BattleEngine` 加 `CurrentOrder`（按需速度混排），`DrawActionBar` 改读它，新回合开始即显示、打速度牌实时刷新。
  - BUG-B：`BattleController.DrawPlayers` 点我方时 `_selectedTarget = p.Id` 设成自己，多角色同动时单体伤害打自己、敌人不掉血 → 点我方只改 `_selectedActor`，目标独立选择。
- 收口顺序：修 bug → `CQ/创建战斗测试场景` 目视+playtest 三关 → T12 全量 NUnit + 改 JSON 抽查 → T13 录屏 → T14 核对。
- git 分工：本地 `git commit` 由我执行，远端 push 由用户本地完成。

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

---

*每个阶段完成后或遇到错误时更新此文件。*