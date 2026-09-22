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
| 我在哪里？ | T3–T6 文件完成（4 subagent），待 Unity 编译 + NUnit，然后进 T7 集成 |
| 我要去哪里？ | 剩余实现阶段 |
| 目标是什么？ | 2 周可玩可讲的 2.5D 回合制垂直切片 |
| 我学到了什么？ | 见 findings.md |
| 我做了什么？ | 两轮修订计划，定微细节 |

---

*每个阶段完成后或遇到错误时更新此文件。*