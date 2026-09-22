# 具体任务清单（Tasks）

> 执行/委派用清单，规则与数值口径见 `task_plan.md`。本文件只列：任务、依赖、产出文件、验收、委派方式。
> 状态标记：`[ ]` 待办 / `[~]` 进行中 / `[x]` 完成。
> 子 agent 认领一个 task 后：读「产出 + 验收」→ 实现 → 回写「完成」并在 `progress.md` 记错误/文件改动。

## 依赖图（第 1 周，骨架）

```
T1 脚手架+DTO+配置加载
 └─> T2 回合状态机(排序+队列)   ──┐
     T3 角色三循环(Unit+Damage+9招) ─┤ 并行，但共用契约(见下)
     T4 意图+目标选择             ──┤
     T5 卡牌系统                 ──┤
     T6 状态系统                 ──┘
        └─> T7 串联(胜负+可玩)
```

并行前提：T1 完成后**先冻结 Core 共享契约**（`Unit` 公开字段、`IDamageResolver`/`IStatusResolver`/`ICardEffect` 接口签名），再委派 T3–T6 各自实现，最后 T7 集成。

## 契约（T1 后冻结，任何人不得单方面改签名）

- `Unit`：`Id, Hp, MaxHp, BaseSpeed, Row, AggroWeight, Energy, MaxEnergy, Ult, MaxUlt`；方法 `TakeDamage(int), Heal(int), GainEnergy(int), GainUlt(int), IsDead`。
- `IDamageResolver`：`DamageResult ResolveDamage(in DamageRequest req, CombatContext ctx)`。
- `IStatusResolver`：`void Apply(string unitId, StatusEffect fx, CombatContext ctx)`。
- `ICardEffect`：`void Resolve(CombatContext ctx, string targetUnitId)`。
- `CombatContext`：持有 `List<Unit>`、`Rng`、`Dictionary<string, List<StatusEffect>>`，作为所有 resolver 的唯一上下文。
- 单位 id 统一 `string`（= 配置 id，如 `sword`），所有 `unitId`/`targetUnitId` 均 string。
- 去尾取整统一走 `Mathd.FloorToInt`（Core 层禁止 `UnityEngine.Mathf`）。

## 第 1 周任务（骨架）

### T1 工程脚手架 + 数据驱动 + 前后排骨架

- [x] **T1.1 目录 + asmdef 隔离**
  - 产出：`Core/`、`Runtime/`、`Editor/`、`Tests/EditMode/` 目录 + 各 `.asmdef`（Core 零 `UnityEngine` 引用）。
  - 验收：编译通过；Core 程序集不引用 UnityEngine。
- [x] **T1.2 Config DTO**
  - 产出：`Core/Config/CharacterConfig.cs`（含 `SkillSpec`/`DamageSpec`/`HealSpec`）、`EnemyConfig.cs`（含 `IntentConfig`，已合并）、`CardConfig.cs`、`StatusConfig.cs`、`LevelConfig.cs`、`LoadResult.cs`、`ConfigValidator.cs`（均 `[Serializable]`，`row`/`aggroWeight` 到位）。
  - 验收：字段覆盖 `task_plan.md` schema 全部；可被 JsonUtility 序列化。
- [x] **T1.3 ConfigLoader**
  - 产出：`Runtime/Config/ConfigJson.cs` + `Runtime/Config/ConfigLoader.cs`（JsonUtility 加载 + schema 校验 + 缺字段日志）。JsonUtility 需 UnityEngine，故加载器放 Runtime；Core 只留纯 DTO + `ConfigValidator.cs`（见 findings.md 决策）。
  - 验收：读 5 类样例 JSON 全成功；故意坏 JSON 会报缺字段。
- [x] **T1.4 样例 JSON**
  - 产出：`StreamingAssets/Data/{Characters,Enemies,Cards,Statuses,Levels}/` 下 5 类样例：3 角色 + 6 卡定义（`count` 合计 8 张）+ 3 敌 + 5 状态 + 3 关，共 20 个 JSON。
  - 验收：`ConfigLoaderTests` 加载后断言「3 角色 + 8 卡张 + 3 敌 + 5 状态 + 3 关」，含 row/aggroWeight；坏 JSON 报缺字段。

### T2 回合管理器（依赖 T1）

- [x] **T2.1 SpeedSorter**
  - 产出：`Core/Battle/SpeedSorter.cs`（含 `EffectiveSpeed` 去尾）、`Core/Mathd.cs`（`FloorToInt` 统一入口）。
  - 验收：`SpeedSorterTests` 覆盖「同速我方先」「同方同速稳定序」「提速/降速只改本回合」「有效速度去尾」。
- [x] **T2.2 TurnManager 状态机骨架 + ActionQueue**
  - 产出：`Core/Battle/TurnManager.cs`、`ActionQueue.cs`、`Rng.cs`；`Core/Combat/Unit.cs`、`Team.cs`（契约 Unit 提前落地）。
  - 验收：状态机走完 `RoundStart→排序→行动→DeathCheck→RoundEnd` 空跑不崩；`TurnManagerTests` 覆盖行动顺序、死亡跳过、胜负判定、注入速度改序。

### T3 角色三循环（依赖 T1；并行组 A）

- [x] **T3.1 Unit + 能量/大招条**
  - 产出：`Core/Combat/Unit.cs`（T2 提前落地：字段+方法全齐）。
  - 验收：普攻 +1 能 +1 大招、技能耗 2 能 +1 大招、终结技耗满大招，见 `SkillResolverTests`。
- [x] **T3.2 Damage**
  - 产出：`Core/Combat/Damage.cs`（去尾乘法，攻增→虚弱→护盾，共享契约）。
  - 验收：`DamageTests` 覆盖 66×0.75=49、顺序、护盾挡一次。
- [x] **T3.3 9 招 resolver**
  - 产出：`Core/Combat/SkillResolver.cs`（basic/skill/ult × 3 角色，subagent）。
  - 验收：`SkillResolverTests` 12 测试逐招断言。

### T4 意图 + 目标选择（依赖 T1；并行组 A）

- [x] **T4.1 IntentModel**
  - 产出：`Core/Combat/IntentModel.cs`（subagent）。
  - 验收：`IntentModelTests` 序列循环。
- [x] **T4.2 敌人行动结算**
  - 产出：`Core/Combat/EnemyActionResolver.cs`（attack/multi/attackUp/charge，subagent）。
  - 验收：`EnemyActionResolverTests` 6 测试；charge 第 2 回合释放、被 interrupt 不释放。
- [x] **T4.3 TargetSelector**
  - 产出：`Core/Combat/TargetSelector.cs`（subagent）。
  - 验收：`TargetSelectorTests` 前排权重 > 后排、不返 dead。

### T5 卡牌系统（依赖 T1；并行组 A）

- [~] **T5.1 CardDef + 抽牌/存牌/多出**
  - 产出：`Core/Cards/CardDef.cs`（subagent）；抽牌/手牌管理**移入 T7**（手牌是回合态）。
  - 验收：卡牌 effect 触发见 T5.2；抽牌/存牌/多出在 T7 闭环验收。
- [x] **T5.2 CardEffectResolver（8 张）**
  - 产出：`Core/Cards/CardEffectResolver.cs`（6 效果，subagent）。
  - 验收：`CardEffectResolverTests` 6 测试；打断等效果走 AddStatus。

### T6 状态系统（依赖 T1；并行组 A）

- [x] **T6.1 StatusType + StatusEffect**
  - 产出：`Core/Combat/StatusType.cs`、`StatusEffect.cs`（共享契约，非 Core/Status）。
  - 验收：5 状态枚举 + Duration/Turn/Percent/Amount 齐全。
- [x] **T6.2 StatusResolver（结算时序）**
  - 产出：`Core/Status/StatusResolver.cs`（subagent）。
  - 验收：`StatusResolverTests` 5 测试；打断跳回合、护盾 once、减速/提速只本回合、回合末衰减。

### T7 串联（依赖 T2–T6）

- [x] **T7.1 死亡/胜负/完整闭环**
  - 产出：`Core/Battle/BattleEngine.cs` + `Core/Cards/Deck.cs`；`EnemyActionResolver.ResolveSkipped`（打断只走蓄力释放路径）；`SkillResolver.CanUse`（能量/大招门槛）。
  - 验收：`BattleEngineTests` 6 测试（自动普攻胜利 / 打断跳过并取消蓄力 / 能量门槛 / 抽牌封顶 / 敌方获胜 / 同 seed 同态）。
- [x] **T7.2 最小可玩场景**
  - 产出：`Runtime/BattleController.cs`（IMGUI 选招/打牌/结束回合/重开）+ `Editor/BattleSceneMenu.cs`（菜单 `CQ/创建战斗测试场景`）。
  - 验收：编辑器里从选 3 角色到打穿 1 个临时关。

## 第 2 周任务（粗列，动工时再拆细同格式）

- [x] T8 数值配平 + 3 敌模板 + 3 关编排
  - 产出：杂兵 `baseSpeed 4→7`（教敌我混排）；`IntentConfig.buffDuration`（攻增跨回合，修「attackUp 当回合即失效」平衡坑）；3 敌模板/3 关编排落表现状；`BalanceTests` 4 测试。
  - 验收：`Level1_AutoBasic_IsWinnable`（纯普攻可过）、`Level1_TurnOrder_IsMixed`（混排）、`Lord_Has_Interruptible_Charge`（第 2 关教学）、`All_Wave_Enemy_Ids_Resolve`（引用无缺失）。
- [x] T9 意图完整结算 + 存牌爆发点验证
  - 产出：`IntentFlowTests.cs` 3 测试（AOE 打全体 / 打断后下回合重新亮同意图 / 跨回合存牌+同回合多出）。
  - 验收：蓄力/攻增/AOE/连击结算齐全；打断边界对齐 `战斗设计.md` 第八节；boss 强意图→打断→本回合不出手成立；存牌可同回合多出。
- [ ] T10 2.5D 表现（左侧行动条 / 前后排站位 / 意图图标 / 手牌）
- [ ] T11 打磨 + 数值微调
- [ ] T12 完整回归（NUnit 全量 + 确定性 + 零硬编码抽查）
- [ ] T13 录屏 + 讲稿 + 架构图
- [ ] T14 缓冲 + 验收核对

## 委派建议

- T1 串行一人做完（契约冻结前提）。
- T3/T4/T5/T6 可 4 个 subagent 并行，但**先读契约节 + task_plan.md 结算顺序节**，改动 `Unit`/接口签名前必须先同步。
- T7 一人集成，收口前跑全部 NUnit。
- 每 subagent 完成回写：「完成」+ 新增文件路径 + 遇到的错误（写入 progress.md 错误日志）。