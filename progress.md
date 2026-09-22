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
| 我在哪里？ | T1 完成 + EditMode 全绿，待 T2 |
| 我要去哪里？ | 剩余实现阶段 |
| 目标是什么？ | 2 周可玩可讲的 2.5D 回合制垂直切片 |
| 我学到了什么？ | 见 findings.md |
| 我做了什么？ | 两轮修订计划，定微细节 |

---

*每个阶段完成后或遇到错误时更新此文件。*