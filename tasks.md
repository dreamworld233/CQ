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

- [ ] **T3.1 Unit + 能量/大招条**
  - 产出：`Core/Combat/Unit.cs`（实现契约字段+方法）。
  - 验收：普攻 +1 能 +1 大招、技能耗 2 能 +1 大招、终结技耗满大招，全部按初值正确。
- [ ] **T3.2 Damage**
  - 产出：`Core/Combat/Damage.cs`（物理/魔法、single/all、heal，去尾乘法）。
  - 验收：66×0.75=49；攻增→虚弱→护盾顺序正确。
- [ ] **T3.3 9 招 resolver**
  - 产出：`Core/Combat/SkillResolver.cs`（basic/skill/ult 三型 × 3 角色）。
  - 验收：9 招逐一触发，能量/大招/伤害数值正确。

### T4 意图 + 目标选择（依赖 T1；并行组 A）

- [ ] **T4.1 IntentModel**
  - 产出：`Core/Combat/IntentModel.cs`（意图枚举、序列循环、回合开始亮意图）。
  - 验收：预告与实际行动一致；蓄力第 2 回合才结算。
- [ ] **T4.2 敌人行动结算**
  - 产出：敌人行动 resolver（普攻/蓄力/攻增/AOE/连击）。
  - 验收：5 类敌人行动全部按意图序列结算。
- [ ] **T4.3 TargetSelector**
  - 产出：`Core/Combat/TargetSelector.cs`（受击权重加权随机）。
  - 验收：`TargetSelectorTests` 前排权重 > 后排；单人且嘲讽位不可实时跳票（本期仅权重）。

### T5 卡牌系统（依赖 T1；并行组 A）

- [ ] **T5.1 CardDef + 抽牌/存牌/多出**
  - 产出：`Core/Cards/CardDef.cs`、抽牌与手牌管理。
  - 验收：每回合抽 1、可囤、可一回合多出；打牌窗口仅在回合开始意图亮后。
- [ ] **T5.2 CardEffectResolver（8 张）**
  - 产出：`Core/Cards/CardEffectResolver.cs`（打断/虚弱/降速/提速/护盾/攻增）。
  - 验收：8 张全触发；打断只对「本回合意图已亮」生效。

### T6 状态系统（依赖 T1；并行组 A）

- [ ] **T6.1 StatusType + StatusEffect**
  - 产出：`Core/Status/StatusType.cs`、`StatusEffect.cs`。
  - 验收：5 状态枚举 + 时长/时点字段齐全。
- [ ] **T6.2 StatusResolver（结算时序）**
  - 产出：`Core/Status/StatusResolver.cs`（虚弱去尾、护盾挡一次、减速/提速、打断、回合结束衰减）。
  - 验收：打断后本回合不出手、下回合恢复；护盾只挡一次。

### T7 串联（依赖 T2–T6）

- [ ] **T7.1 死亡/胜负/完整闭环**
  - 产出：完整回合状态机闭环。
  - 验收：`TurnManagerTests` 跑完一局（输入假指令），胜负判定正确。
- [ ] **T7.2 最小可玩场景**
  - 产出：`Runtime/BattleController.cs`（临时素材 + 控制台/简单 UI 交互）。
  - 验收：编辑器里从选 3 角色到打穿 1 个临时关。

## 第 2 周任务（粗列，动工时再拆细同格式）

- [ ] T8 数值配平 + 3 敌模板 + 3 关编排
- [ ] T9 意图完整结算 + 存牌爆发点验证
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