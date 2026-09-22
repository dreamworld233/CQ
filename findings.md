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

## 已定微细节（用户确认）
- 能量初值：`maxEnergy=3`、`maxUlt=3`；普攻 +1 能 +1 大招，技能耗 2 能 +1 大招，终结技耗满大招。后续可改。
- 双人 LAN：本期不做，第二板块。
- 行动条：显示出手顺序，放画面左侧。
- 前后排：只做框架（row + 受击权重，前排更易受击），差异玩法后续扩展。

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
- 待 T8/T9 平衡隐患：杂兵 `attackUp` 意图 `Duration=1 round` 于其行动当回合生效、回合末即失效，当前实战无可受益伤害。T8 数值/意图节奏一并修正。

## 资源
- `demo任务清单.md`（需求源头）
- `战斗设计.md`（战斗规则源头）
- `Assets/Settings/Renderer2D.asset`（2D 渲染）

---

*每执行 2 次查看/浏览器/搜索操作后更新此文件。*