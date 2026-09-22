# 任务计划：2.5D 回合制战斗垂直切片（2 周）

> 依据 `demo任务清单.md` + `战斗设计.md`。本文件补充架构、数据 schema、确定性结算顺序、逐日实现细节、测试策略与风险。
> AI 内容管线降为「提效」**可选待办**，不作为本期验收。主目标 = 游戏完整逻辑。
> 冲突口径：规模以 `demo任务清单.md` 为准；战斗规则以 `战斗设计.md` 为准。

## 目标

2 周交付可玩、可面试讲解的 2.5D 回合制战斗垂直切片：3 角色、≤3 敌、8 卡、5 状态、3 关，异步回合，速度混排 + 意图反制 + 前后排框架。完成度 > 复杂度。

## 冻结规模（不扩）

- 3 角色（剑士/护卫/术士），每角色 3 招（普攻/技能/终结技）= 9 招。
- ≤3 敌（杂兵/爪牙/领主），卡池 8 张，状态 5 种，3 关编排：群怪 / 单 boss / boss+爪牙。
- 平台：Android 横屏为基准；PC 编辑器留作录屏/验收底盘（测试仍在 PC 跑）。
- 同步模型：异步回合（轮流行动），本地单机。双人 LAN / 观战回放 = 第二板块，本期**不做**（已确认）。
- AI 管线：本期只体现「AI 提效」（可选），不做生成-模拟-回写闭环。
- 数值：全部走 JSON / Asset 配置，代码**零硬编码数值**。

## 规则精要（来自 `战斗设计.md`）

- 小回合 = 我方 3 角色 + 敌方 ≤3 怪各行动一次；出手顺序 = 双方速度混排，高先动，同速 tie-break**我方先**。
- 速度卡牌（降速/提速）只影响本小回合排序；**不做**速度阈值 / 拉条。
- 抽牌：每回合开始抽 1，可存，可多出。
- 打断边界：只对「本回合意图已亮」的怪生效；打断后本回合不出手，下回合重新亮意图。
- 角色循环：普攻回能量**并顺带攒大招条** → 技能耗能量 → 终结技耗满大招条。
- 意图跨单位预告：回合开始即亮，玩家据此打降速 / 提速 / 打断。

## 已定微细节（用户确认）

| 项 | 结论 |
|----|------|
| 能量循环初值 | `maxEnergy=3`、`maxUlt=3`；普攻 +1 能量 +1 大招条；技能耗 2 能量 +1 大招条；终结技耗满大招条（3）。初值，后续可调 |
| 双人 LAN | 本期不做，属第二板块 |
| 行动条 | 显示**出手顺序**，UI 放画面**左侧** |
| 前后排 | 只做**框架**：每单位带 `row`（front/back）；敌人单体攻击按受击权重选目标，前排默认权重更高，预留嘲讽等扩展点；本期不做具体前后排差异逻辑 |

## 技术基线（已确认）

- 引擎：Unity 2022.3.62f2（`ProjectSettings/ProjectVersion.txt`）。
- 渲染：URP 14.0.12（2D Renderer，`Assets/Settings/Renderer2D.asset`）。
- 测试：`com.unity.test-framework` 1.1.33 已装，EditMode NUnit 跑纯逻辑。
- JSON：内置 `JsonUtility`（`com.unity.modules.jsonserialize`），v0 不引入第三方 JSON 库。
- 版本控制：git 2.52.0，全局 user 已配（`dreamworld233`）。

## 架构决策

| 决策 | 理由 |
|------|------|
| 战斗逻辑放纯 C# `Core` 层，零 `UnityEngine` 依赖 | 可被 EditMode NUnit 直接测试；为后续 AI 提效预留 headless 复用 |
| 配置用 JSON（StreamingAssets），战斗数值不用 ScriptableObject | 数据驱动，零硬编码；ScriptableObject 只做关卡索引/引用壳 |
| 确定性：固定随机种子 + 不依赖 `Update`/帧时间 | 同 seed 重放结果一致 |
| 战斗状态机为显式确定性步进 | 每步可单步调试 |
| 前后排用「row + 受击权重」表达，不写死规则 | 前排更易受击是通用规则，权重可扩展嘲讽/潜行 |
| 无第三方 JSON/网络库 | 本期无网络、无在线 LLM |

## 目录与命名约定

```
Assets/
  Scripts/
    Core/                       # 纯 C#，零 UnityEngine 依赖
      Config/                   # 只读 DTO + 校验
        CharacterConfig.cs  EnemyConfig.cs  CardConfig.cs
        StatusConfig.cs     LevelConfig.cs  LoadResult.cs  ConfigValidator.cs
      Battle/
        TurnManager.cs          # 回合状态机
        ActionQueue.cs          # 排序后的行动队列
        SpeedSorter.cs          # 有效速度计算 + tie-break
        Rng.cs                  # 确定性伪随机（可注入 seed）
      Combat/
        Unit.cs  Team.cs  Damage.cs  IntentModel.cs  TargetSelector.cs
      Cards/
        CardDef.cs  CardEffectResolver.cs
      Status/
        StatusType.cs  StatusEffect.cs  StatusResolver.cs
    Runtime/                    # MonoBehaviour 桥接 + 表现层 + JSON 反序列化
      ConfigJson.cs  ConfigLoader.cs
      BattleController.cs  UnitView.cs
      ActionBarUI.cs  IntentIconUI.cs  HandUI.cs  CameraController.cs
      PositioningView.cs        # 前后排站位表现
    Editor/                     # 仅测试/关卡调试菜单，无 LLM
    StreamingAssets/
      Data/  Characters/  Enemies/  Cards/  Statuses/  Levels/
  Tests/
    EditMode/                  # NUnit，只引用 Core
      SpeedSorterTests.cs  DamageTests.cs  StatusTests.cs
      TurnManagerTests.cs  DeterminismTests.cs  TargetSelectorTests.cs
```

约定：`Core` 内禁止 `UnityEngine`/`MonoBehaviour`；`Runtime` 是唯一引擎桥接；配置 ID 统一小写下划线。

## 数据 schema（v0，JsonUtility 可反序列化扁平 DTO）

### 角色 `Characters/*.json`

```json
{
  "id": "sword",
  "name": "剑士",
  "role": "dps",
  "maxHp": 100,
  "baseSpeed": 10,
  "maxEnergy": 3,
  "maxUlt": 3,
  "row": "front",
  "aggroWeight": 2,
  "skills": [
    { "id": "sword_basic", "name": "普攻", "type": "basic",
      "energyDelta": 1, "ultDelta": 1,
      "damage": { "kind": "physical", "target": "single", "amount": 12 } },
    { "id": "sword_skill", "name": "技能", "type": "skill",
      "energyCost": 2, "ultDelta": 1,
      "damage": { "kind": "physical", "target": "single", "amount": 30 } },
    { "id": "sword_ult", "name": "终结技", "type": "ult",
      "damage": { "kind": "physical", "target": "all", "amount": 45 } }
  ]
}
```

- `type ∈ {basic, skill, ult}`；`basic` 回能量+攒大招条，`skill` 耗能量，`ult` 耗满大招条。
- `damage.target ∈ {single, all}`、`kind ∈ {physical, magic}`；治疗用显式 `heal` 字段。
- 前后排：`row ∈ {front, back}`，`aggroWeight` 受击权重（前排默认更高）。配比：剑士/护卫前排、术士后排。

### 敌人 `Enemies/*.json`

```json
{
  "id": "lord",
  "name": "领主",
  "maxHp": 300,
  "baseSpeed": 5,
  "row": "front",
  "aggroWeight": 2,
  "intentSequence": ["basic", "aoe", "charge"],
  "intents": {
    "basic":  { "type": "attack",    "target": "single", "damage": 40 },
    "aoe":    { "type": "attack",    "target": "all",    "damage": 25 },
    "charge": { "type": "charge",    "nextDamage": 120,   "interruptible": true }
  }
}
```

约束：`intentSequence` 长度 2~4，元素必须存在于 `intents`；`charge` 必须 `interruptible: true`。

### 前后排与目标选择（框架，非完整玩法）

- 每个单位有 `row` 与 `aggroWeight`；单体攻击目标由 `TargetSelector` 决定。
- 默认规则：在存活且合法的目标中，按 `aggroWeight` 加权随机；前排权重更高 → 常规状态下更易受击。
- 扩展点预留：嘲讽（强制指向某单位）、潜行（不可被单体选中）、位移（换排）。本期不实现。

### 卡牌 `Cards/*.json`

```json
{ "id": "interrupt", "name": "打断", "effect": { "type": "interrupt" }, "count": 2 },
{ "id": "weaken",   "name": "虚弱", "effect": { "type": "weaken", "percent": 25 }, "count": 2 },
{ "id": "slow",     "name": "降速", "effect": { "type": "slow", "percent": 50 }, "count": 1 },
{ "id": "haste",    "name": "提速", "effect": { "type": "haste", "percent": 50 }, "count": 1 },
{ "id": "shield",   "name": "护盾", "effect": { "type": "shield", "amount": 20 }, "count": 1 },
{ "id": "attackUp", "name": "攻增", "effect": { "type": "attackUp", "percent": 30 }, "count": 1 }
```

### 状态 `Statuses/*.json`

```json
[
  { "id": "weaken", "duration": 1, "turn": "round" },
  { "id": "shield", "duration": 1, "turn": "once" },
  { "id": "attackUp", "duration": 1, "turn": "round" },
  { "id": "slow", "duration": 1, "turn": "round" },
  { "id": "interrupt", "duration": 1, "turn": "round" }
]
```

### 关卡 `Levels/*.json`

```json
{ "id": 1, "waves": [ { "enemies": ["grunt", "grunt", "grunt"] } ] }
```

## 核心战斗循环与确定性结算顺序

回合状态机（`TurnManager`）：

```
RoundStart → 亮意图 + 抽牌 → 玩家打牌/选招 →
SpeedSort（有效速度） → 按序结算每个单位（单体目标经 TargetSelector） → DeathCheck →
RoundEnd（状态衰减） → 胜负判定 → 下一回合 或 结束
```

结算顺序（固定，任何层不得绕过）：

1. **打牌窗口**（回合开始、意图已亮）：打断 / 虚弱 / 降速 / 提速 / 护盾 / 攻增即时标记，不立即扣血。
2. **速度混排**：`有效速度 = baseSpeed × (1 + 提速% − 降速%)`；同速 tie-break **我方先**；同方同速按稳定序（注册序）。速度修正只影响本回合排序，不跨回合。
3. **行动结算**：从队列首到尾；被「打断」的单位跳过本回合，下回合重新亮意图；单体攻击经 `TargetSelector` 选目标（受击权重），AOE 打全体。
4. **伤害结算**（乘法打在落地伤害，每次乘法后**去尾**）：
   `落地伤害 = 基础伤害 × 攻增系数 → 去尾 → × 虚弱系数 → 去尾 → 护盾吸收 → 扣 HP`。
   例：66 × 0.75 = 49.5 → 49。
5. **死亡结算**：HP≤0 移除；我方全灭判负、敌方全灭判胜。
6. **回合结束**：持续 1 回合状态衰减；「打断」清空。

确定性要求：`Rng` 可注入 seed；伤害、抽牌、意图循环、目标选择全走 `Rng`；禁止读 `Time`/`frameCount`。

## 逐日详细拆解（14 天，每天一验收）

### 第 1 周：战斗骨架可玩（临时素材打穿一局）

- **D1 工程脚手架 + 数据驱动配置框架 + 前后排骨架**
  - 产出：`Core/Config/*.cs` DTO（含 `row`/`aggroWeight`）、`ConfigLoader.cs`、5 类样例 JSON、目录骨架、`.asmdef` 隔离。
  - 关键点：数值零硬编码；schema 校验并打印缺字段；Core 无 `UnityEngine` 引用。
  - 验收：日志打印「3 角色 + 8 卡 + 3 敌 + 5 状态 + 3 关」全读出，字段含 `row`/`aggroWeight`。

- **D2 回合管理器：速度混排 + 行动队列**
  - 产出：`TurnManager.cs`、`SpeedSorter.cs`、`ActionQueue.cs`。
  - 关键点：有效速度、同速 tie-break 我方先、稳定序。
  - 验收：`SpeedSorterTests` 覆盖 tie-break 与稳定序；打印行动队列与预期一致。

- **D3 角色三循环：9 招 + 能量/大招条**
  - 产出：`Unit.cs`、`Damage.cs`、技能 effect 解析。
  - 关键点：普攻回能量+攒大招条、技能耗能量、终结技耗满大招；能量/大招条变更集中一处。
  - 验收：9 招逐一触发，能量/大招条/伤害按初值正确。

- **D4 敌人意图系统 + 目标选择：枚举 + 亮意图 + 行动结算**
  - 产出：`IntentModel.cs`、意图序列循环、敌人行动结算（普攻/蓄力/攻增/AOE/连击）、`TargetSelector.cs`。
  - 关键点：回合开始即亮全部意图；蓄力次回合才结算；单体攻击按受击权重选目标（前排更易受击）。
  - 验收：预告与实际一致；蓄力第 2 回合命中；`TargetSelectorTests` 验证前排权重高于后排。

- **D5 卡牌系统：抽牌 + 存牌 + 8 卡效果**
  - 产出：`CardDef.cs`、`CardEffectResolver.cs`。
  - 关键点：每回合抽 1、可囤、可多出；打牌窗口只在回合开始且意图亮后。
  - 验收：8 张效果全触发；打断只对「本回合意图已亮」生效。

- **D6 状态系统：5 状态 + 统一结算顺序**
  - 产出：`StatusType.cs`、`StatusEffect.cs`、`StatusResolver.cs`。
  - 关键点：虚弱去尾、护盾挡一次、减速/提速改速度、打断本回合不出手；生效时点可枚举。
  - 验收：66×0.75=49；护盾只挡一次；打断后本回合不出手下回合恢复。

- **D7 串联合一：死亡 → 胜负 → 一局跑通**
  - 产出：完整回合状态机闭环 + 最小可玩场景（临时素材 + 控制台交互）。
  - 验收：从「选 3 角色」到「打穿 1 个临时关」完整跑通。

### 第 2 周：内容 + 表现 + 打磨

- **D8 数值配平 + 3 敌模板 + 3 关编排落表**
  - 产出：角色/敌人完整数值表、3 关卡 JSON。
  - 关键点：关 1 教速度排序、关 2 教打断蓄力、关 3 教多意图抉择。
  - 验收：3 关可过，教学点分别成立。

- **D9 意图完整结算 + 存牌爆发点验证**
  - 产出：蓄力 / 攻增 / AOE / 连击完整结算 + 打断边界对齐 `战斗设计.md` 第八节。
  - 验收：boss 强意图→打断→本回合不出手 联动成立；合理存牌可拆大招。

- **D10 2.5D 表现最小可用（行动条在左侧）**
  - 产出：`BattleController.cs`、`UnitView.cs`、`PositioningView.cs`（前后排站位）、`ActionBarUI.cs`（左侧出手顺序条）、`IntentIconUI.cs`、`HandUI.cs`、`CameraController.cs`。
  - 验收：镜头、左侧行动条、意图图标、手牌均可视可交互；前后排站位可视（不要求差异玩法）。

- **D11 打磨 + bug + 数值微调**
  - 产出：三关手玩平衡、修复残留 bug、可选伤害飘字。
  - 验收：三关单轮体验顺滑，无卡死/崩溃。

- **D12 完整回归验证**
  - 产出：EditMode NUnit 全量跑绿 + 确定性复验 + 数据驱动抽查（无硬编码数值）。
  - 验收：核心规则逐条有测试；同 seed 两次结果一致。

- **D13 录屏 + 面试讲稿 + 架构图**
  - 产出：2~3 分钟录屏、一页架构图、30 秒口播、一句话可讲性。
  - 验收：讲稿串起「速度混排 / 意图压向 / 卡牌反制 / 前后排权重」；架构图三件套（数据驱动 → 确定性状态机 → 测试）。

- **D14 缓冲 + 验收清单核对**
  - 逐条核对文末验收标准；修残留；补遗漏交付物。

## 测试策略

- **单元测试（EditMode NUnit，只引用 Core）**：排序 tie-break、去尾乘法、状态时序、能量/大招条、8 卡效果、意图循环、打断边界、目标选择权重。
- **确定性测试**：同 seed 跑两次，结果逐字段一致。
- **验收对照**：文末「验收标准」逐条打勾，缺一不可交付。

## 关键问题（已决 / 待办）

- 能量初值、双人 LAN、行动条、前后排 —— 均已定（见「已定微细节」）。
- **GitHub 仓库重置**：`https://github.com/dreamworld233/CQ.git` 为废弃版本，待重置（本地 `git init` + 首次提交 + `push --force` 覆盖远程）。执行前需确认远程认证可用。

## 已做决策

| 决策 | 理由 |
|------|------|
| JSON + JsonUtility，无第三方库 | 内置够用，数据驱动 |
| 纯逻辑 Core 与引擎隔离 | headless 测试复用，可测性 |
| 确定性状态机 | 结果可复现 |
| AI 管线降为可选提效 | 用户定调：主目标是游戏完整逻辑 |
| 本期本地异步单机 | 任务清单冻结规模 + 用户确认 |
| 前后排只做框架（row + 受击权重） | 用户定调：先框架，差异玩法后续扩展 |
| JSON 反序列化放 Runtime，Core 只留纯 DTO + 校验 | JsonUtility 属 UnityEngine 模块，保 Core 零引擎引用 |
| `intents` 数组带 id、每实体一 JSON 文件 | JsonUtility 不支持 Dictionary/顶层数组 |
| 卡池 8 张 = 6 独特定义，`count` 合计 8 | 任务清单 8 卡与 schema 6 效果一致 |

## 遇到的错误

| 错误 | 尝试次数 | 解决方案 |
|------|---------|---------|
| - | - | - |

## 待办（不占本期验收）

- **GitHub 仓库重置**：重置废弃远程仓库，让本地新项目成为干净起点。
- **AI 提效（可选）**：LLM 生成一份敌人配置 JSON，游戏读入可进局，只证明「AI 能提效内容生产」。
- **自动战斗模拟闭环（可选）**：headless 跑 N 局出胜率，作平衡辅助。
- **双人 LAN / 观战回放**：第二板块。

## 备注

- 每天更新本文件当天产出/验收，`pending → in_progress → complete`。
- 重大决策前重读本文件；错误写入「遇到的错误」表。