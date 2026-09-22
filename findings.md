# 发现与决策

## 需求
- 依据 `demo任务清单.md`（任务+验收）+ `战斗设计.md`（战斗规则）。
- 3 角色 × 3 招 = 9 招；卡池 8 张；状态 5 种；敌人 3 类；关卡 3 个。
- 异步回合（轮流行动），速度混排决定出手顺序，同速 tie-break 我方先。
- 卡牌反制为主：打断、虚弱、降速、提速、护盾、攻增。
- AI 管线本期只体现「AI 提效」（可选），主目标 = 游戏完整逻辑。
- 数值全部走 JSON / Asset 配置，代码零硬编码。

## 研究发现
- 引擎：Unity 2022.3.62f2；渲染：URP 14.0.12 + 2D Renderer。
- 测试框架 `com.unity.test-framework` 1.1.33 已装。
- JSON 库：内置 `JsonUtility`（不能反序列化顶层数组 / Dictionary，字段需 public + `[Serializable]`）。
- git 2.52.0，全局 user `dreamworld233`（已配）；`gh` CLI 未装；本地非 git 仓库。
- 远程 `https://github.com/dreamworld233/CQ.git` 为废弃版本，待重置。
- Android 环境（已查验）：编辑器 `G:\unity\Unity_2022.3.62f2` 的 AndroidPlayer 模块已装，内置 SDK `android-34/35/36` + build-tools `34.0.0` + NDK `23.1.7779620`(r23b) + OpenJDK `11.0.14.1` Temurin，全齐。外部 `G:\Android\AndroidSdk` 缺 ndk/cmdline-tools，不接 Unity。`JAVA_HOME=G:\jdk21`(JDK21)，构建须走内置 JDK11。Unity Build Settings 切 Android 无 `Unable to locate SDK` 报错（用户确认）。

## 已定微细节（用户确认）
- 能量初值：`maxEnergy=3`、`maxUlt=3`；普攻 +1 能 +1 大招，技能耗 2 能 +1 大招，终结技耗满大招。后续可改。
- 双人 LAN：本期不做，第二板块。
- 行动条：显示出手顺序，放画面左侧。
- 前后排：只做框架（row + 受击权重，前排更易受击），差异玩法后续扩展。

## 平台决策（用户确认）
- 平台以 **Android 横屏**为基准；PC 编辑器留作录屏 + 验收底盘，EditMode 测试仍在 PC 跑。
- 理由：证明移动工程能力（构建链/触控/性能/真机调试），横屏复用现有左右战场布局改动最小。
- 待办移动适配：方向锁定横屏、`CanvasScaler` Scale With Screen Size、触控热区、Android 返回键、`OnApplicationPause/Focus` 保战斗状态。

## 战斗表现修订（用户确认）
- 敌人意图三分类显示：攻击=(显示谁+伤害)、自增益=`↑`、给我方减益=`↓`。
- 单体攻击/连击/蓄力/减益在亮意图时预锁目标（`BattleEngine.GetIntentTarget`），结算用锁定目标，已死回退 `TargetSelector`；同 seed 仍确定。
- 新增敌方减益意图 `debuff`（对锁定玩家上 `weaken`）；`grunt` 序列 `[basic, attackUp, debuff]`。
- 治愈可选友方：`guard`「守护」heal.target `self`→`single`；`BattleEngine.ChooseSkill` 加目标合法性（单治疗须友方、单伤害须敌方）。
- `BattleController` 加「友方目标」按钮区；选治愈时目标非友方自动置回自己；意图文字报预锁目标名。

## 战斗规则要点（源：`战斗设计.md`）
- 小回合 = 我方 3 + 敌方 ≤3 各行动一次。
- 速度卡牌只改本小回合排序，不做阈值/拉条。
- 抽牌：每回合抽 1，可存，可多出。
- 打断边界：只对「本回合意图已亮」生效，打断后本回合不出手，下回合重新亮意图。

## 技术决策
| 决策 | 理由 |
|------|------|
| 战斗逻辑纯 C# `Core` 层，零 `UnityEngine` 依赖 | 可被 EditMode NUnit 测试，避免与表现层耦合 |
| 配置 JSON（StreamingAssets），数值不用 ScriptableObject | 数据驱动，零硬编码 |
| 确定性（固定 seed，不依赖帧） | 同 seed 重放一致，测试可复现 |
| 前后排用 row + 受击权重表达 | 通用规则，预留嘲讽/潜行扩展 |
| AI 管线降为可选待办 | 用户定调：先实现游戏逻辑 |
| 无第三方 JSON/网络库 | 本期无网络、无在线 LLM |
| JSON 反序列化放 Runtime（`ConfigJson`/`ConfigLoader`），Core 只留纯 DTO + `ConfigValidator` | JsonUtility 属 `UnityEngine.JSONSerializeModule`，与 Core「零引擎引用」冲突；纯 DTO 仍可被外部 headless 解析器复用 |
| `intents` 用数组带 `id`（非 map）、每实体一 JSON 文件（非顶层数组） | JsonUtility 无 Dictionary/顶层数组支持 |
| 卡池 8 张 = 6 个独特卡定义，`count` 合计 8 | 任务清单 8 卡与 schema 6 种效果一致 |
| 单位 id 统一 string（= 配置 id） | 配置 id 是字符串；契约原 `int unitId` 与配置冲突，改 string，`Dictionary<string,List<StatusEffect>>` |

## 遇到的问题
| 问题 | 解决方案 |
|------|---------|
| 初版把 AI 闭环当验收核心 | 降为可选提效，D9/D10 换成游戏逻辑任务 |
| `战斗设计.md` 曾缺失 | 用户补录，现为规则源头 |
| 计划把 `ConfigLoader.cs` 放 Core，但 JsonUtility 需 UnityEngine | 加载器移到 Runtime，Core 只保留纯 DTO + 校验（见技术决策） |

## T7 集成积累
- 敌人实例 id = `配置id_序号`（如 `grunt_0`），`Unit.Id` 全局唯一；意图模型每实例一份。关卡 `enemies` 可重复。
- 打断跳回合用 `EnemyActionResolver.ResolveSkipped`：只走蓄力释放路径，`interruptible` 蓄力被取消（拆大招已成立）。
- T8 已修：`IntentConfig.buffDuration` 承接攻增跨回合（杂兵 `attackUp` 设 `buffDuration:2`），EnemyActionResolver 不再硬编码 `Duration=1`；`ConfigValidator` 补 attackUp 校验。

- T9 验证：AOE（`attack target=all`）此前无测试，已补；打断跳过不推进意图游标，下回合重新亮同一意图（对齐 `战斗设计.md` 第八节）。
- 表现层待办（并入 T10）：蓄力待释放的 120 伤害不在下回合意图图标出现，可在敌人头顶加「待释放蓄力」角标做二次预告。

## 零硬编码审计（T12 抽查 · Core 层）
- 平衡数值（伤害/HP/速度/百分比/count/能量成本）全在 JSON，代码不落表值。✅
- 保留在代码的是「规则常量」，非平衡数值，逐条已注释：
  - 卡牌/状态持续 `Duration = 1`（打断/虚弱/降速/攻增=持续到回合末；护盾=`once` 挡一次）——机制固定，非可调数值。
  - 每回合抽 1 张（`BattleEngine.Deck.Draw(1)`）——规则固定。
  - 起手能量/大招条 = 0；百分比 `÷100`；同速 tie-break 我方先；`aggroWeight<=0` 视为 1——均为规则，非数据。
  - `Rng` xorshift 常数、`Team` 枚举 0/1——算法/顺序，非游戏数值。
- 已知非理想项：`Statuses/*.json` 的 `duration/turn` 目前仅作文档与校验，卡牌/敌人施加状态时未回读该值（因所有状态本设计就是持续 1 回合）。留待后续若加「持续 N 回合」状态再接线。

## 待修 bug（用户实测，T11 修）

### BUG-A 行动条不显示「本回合当前顺序」——已修
- 现象：行动条只在 `结束回合` 后显示上一回合已结算顺序；新回合开始不显示本回合出手顺序。
- 修法：`BattleEngine` 加按需计算的 `CurrentOrder`（`SpeedSorter.Sort(Ctx.Units 存活, EffectiveSpeedOfUnit)`）；`BattleController.DrawActionBar` 改读 `CurrentOrder`。回归测试 `CurrentOrder_Available_DuringInput` 已加。

### BUG-B 多角色同回合选招 → 敌人伤害丢失——已修
- 现象：手动给多个角色选普攻，敌人几乎不掉血（目标被设成自己）。
- 修法：`DrawPlayers` 点我方只设 `_selectedActor`，删掉 `_selectedTarget = p.Id;`（目标独立保留上一个，不设自己）。

## 资源
- `demo任务清单.md`（需求源头）
- `战斗设计.md`（战斗规则源头）
- `Assets/Settings/Renderer2D.asset`（2D 渲染）

---

*每执行 2 次查看/浏览器/搜索操作后更新此文件。*