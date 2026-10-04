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

### Android 横屏适配落点（阶段 17）
- 方向锁定横屏：`BattleController.Start` 设 `Screen.orientation = LandscapeLeft`；`CQ/配置 Android PlayerSettings` 菜单设 `defaultInterfaceOrientation = LandscapeLeft`。
- PlayerSettings：包名 `com.dreamworld233.cq`、`AndroidArchitecture.ARMv7 | ARM64`、minSdk 22、IL2CPP（`Editor/AndroidBuildMenu.cs`）。
- Android 返回键：`BattleController.Update` 读 `KeyCode.Escape` → `Application.Quit()`。
- `CanvasScaler` **不适用**：当前全部 IMGUI（OnGUI），无 uGUI Canvas；用 Screen 坐标 + 固定逻辑尺寸自适应即可。
- `OnApplicationPause/Focus` **无需代码**：战斗是纯确定性状态机、零帧/协程依赖，状态在 C# 内存里切后台天然保留（进程被杀则退出，属系统行为）。
- 已知非理想项：IMGUI 按钮热区为固定像素，高 DPI 手机上偏小；后续若要大热区，迁 uGUI 或调大字体/边距。

## 真机空屏 bug（阶段 18 诊断 → 阶段 19 已修，待真机验）
- 现象：Android 真机只显示蓝色背景（相机 clear），无世界单位、无 HUD、无可交互内容；Play 在编辑器内一切正常。编译/测试/打包均绿。
- 候选根因 1（强证据）：`ConfigJson.LoadAll` 用 `Directory.Exists`/`File.ReadAllText` 读 `Application.streamingAssetsPath`。Android 上该路径在 APK 内（`jar:file://.../base.apk!/assets`），System.IO 读不到 → 每个目录报「目录不存在」→ `NewBattle` 置 `_configError` 后 `return`，不 `SpawnWorld()` → 无单位。PC/编辑器是真实文件系统所以正常。
- 候选根因 2（场景持久化）：`CQ/创建战斗测试场景` 菜单只在当前打开场景内存里 `AddComponent<BattleController>`，不 `EditorSceneManager.MarkSceneDirty` 也不保存。若用户跑完菜单没 Ctrl+S 存场景，Build 用的 SampleScene 磁盘版无 BattleController → 真机什么都不起。

### 阶段 19 修法（已落地代码，待 Unity 编译 + 真机验）
- 根因 1 选选项 B：JSON 从 `Assets/StreamingAssets/Data` 迁到 `Assets/Resources/Data`（20 JSON + .meta 全保留，GUID 不变）。
  - `ConfigJson` 新增 `LoadAllFromResources<T>(resourceFolder, validate)`：`Resources.LoadAll<TextAsset>` 同步读，真机/编辑器一致；原文件系统版 `LoadAll` 保留（`ConfigLoaderTests.Bad_Json_Reports_Error` 仍用）。
  - `ConfigLoader` 改用 `LoadAllFromResources`，路径 `Data/Characters` 等；删 `DataRoot`/`System.IO` 引用。
- 根因 2 兜底：`BattleController` 加 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`，场景无 `BattleController` 时 `new GameObject().AddComponent<BattleController>()`，不依赖编辑器保存。
- 验证待办：Unity 编译 → 全量 EditMode（应仍全绿，含 `Loads_All_Sample_Data` 改走后仍读 Resources）→ 重打包 APK → 真机/MuMu 验「有世界单位 + HUD + 横屏 + 触控 + 返回键」。
- 说明：IMGUI（OnGUI）在 Android 构建里可渲染（非蓝屏主因），但仍是结构隐患；真正上移动端建议迁 uGUI。修上面两个根因后应先验证「有世界单位 + HUD」。

## 点击无反馈 bug（阶段 20 待修，下午）
- 现象：编译无报错；Play 后世界单位 + HUD 可见，但点角色无高亮环、无弧形技能菜单。交互链路「点选 → 选中 → 环形」链路不响应。
- 候选根因（按可能性排序，待下午日志定位）：
  1. **点击未命中单位**：`Physics2D.OverlapPoint` 未命中 `BoxCollider2D`。可能 2D 物理 layer 未建、或 `ScreenToWorldPoint` 世界点换算偏差、或 `EventSystem.IsPointerOverGameObject` 误判 true 把点击吞掉。
  2. **命中但环形画到屏外**：`RectTransformUtility.ScreenPointToLocalPointInRectangle` 与 `CanvasScaler` 缩放坐标错位，按钮位置算到屏外 → 选中了但看不到环形。
  3. **高亮环未刷**：`UnitView.SetHighlight` 环子物体未显示。
- 下午 debug 计划（先打日志再改）：
  - `HandleUnitClick` 入口打一条（确认 `GetMouseButtonDown` 触发）；`PointerOverUi()` 返回值打一条；`OverlapPoint` 命中/null 打一条。
  - `OnUnitTapped` 打角色 id；`ShowRadial` 打 anchor 世界坐标 + 技能数。
  - `RadialSkillMenu.WorldToCanvas` 打 world / screen / local 三点，看按钮落点是否在屏内。
  - 用日志二分：click 没进 → 物理/事件系统；进了但没命中 → 碰撞体；命中但没环形 → 坐标换算。

## 点选链路修复（阶段 22，待 Play 验证）
### 真正根因（用户实测后定位）：`Rect()` 锚点对调 → 结束回合按钮变近全屏遮罩
- `BattleUi.BuildBottomControls` 里三处调用写成了 `Rect(rt, 1f, 1f, 0f, 0f, ...)`，而 `Rect(rt, ax, ay, bx, by, ...)` 的 `bx/by` 是 `anchorMax` → `anchorMin=(1,1)`、`anchorMax=(0,0)`，**锚点反向**。
- 反向锚点 = 拉伸矩形：`offsetMin = anchoredPosition - sizeDelta*pivot = (-336,-94)`、`offsetMax = (-36,36)`，实际矩形 x∈[-36,1584]、y∈[36,986]（1920×1080）。整个战场都在这块里。
- 后果 1：`PointerOverUi()` 恒为 true → `HandleUnitClick` 每次提前 return → **点任何单位都无反馈、无弧形菜单**（即阶段 20 报的 bug）。
- 后果 2：该按钮本身被点中 → `OnEndRound` → **点空白处回合自动推进**。
- 为何技能圆钮还能点：它们在 Canvas 子节点里更靠后，绘制与射线都在上层，未被这块遮罩挡住。
- 修法：三处改为 `Rect(rt, 1f, 0f, 1f, 0f, ...)`（右下，pivot 同锚点）/ `0.5f,0.5f,0.5f,0.5f`（居中）。**规则：`Rect` 的四个锚点参数必须成对相同。**
- 教训：`RectTransform` 的 `anchorMin > anchorMax` 不报错，静默变成反向拉伸矩形；这类「隐形全屏遮罩」会让 `IsPointerOverGameObject` 恒真。以后凡是「点不到」，先怀疑有没有全屏 raycast 目标。

### 同时修的确定缺陷
- **命中取「最近」而非数组第一个**：`PickUnitCollider(hits, point)` 按到点击点距离决胜。原先取 `OverlapPointAll` 的第一个，多个碰撞体同时命中时数组顺序固定 → 某个角色永远选不中（用户报「选不中法师」）。
- **单位间距 < 碰撞体宽**：`PositioningView` 原 `TeamSpacing = 1.4`，而碰撞体是世界 2.21 宽（`BoxCollider2D` 1.7 × 根节点 scale 1.3）→ 同排（三只怪全是 `row: front`）碰撞体重叠，点谁都会命中多个（用户报「三个怪物过于紧凑、无法具体选中」）。修法：`SpawnUnit` 不再缩放根节点（`localScale = Vector3.one`，缩放会连碰撞体一起放大）；间距参数化为 `BattleLayout.teamGap = 2.0`；碰撞体缩到 `1.4 × 1.7` → 同侧间距 2.0 > 宽 1.4，互不重叠。
- **左侧行动条盖住最左敌人**：原面板宽 400（屏幕 x 26..426 ≈ 世界 x −8.65..−4.94），最左怪在世界 x −5.0 处被压住。修法：面板收到 340 宽 + `Image.raycastTarget = false`（纯展示，不再吞点击）+ 阵型整体右移 `arenaShift = 0.9`。
- **相机不保证正交**：`EnsureCamera` 原先 `if (Camera.main != null) return Camera.main;` 直接复用场景相机。`SampleScene` 自带 MainCamera，若是透视相机则 `ScreenToWorldPoint(mousePosition)` 落点不在世界 z=0 平面 → x/y 全偏。改为**强制** `orthographic = true` + `size = 5` + 归位 `(0,0,-10)` + 实心底色。
- **`Physics2D` 查询的默认 layerMask 陷阱**：`OverlapPoint(point)` 默认用 `DefaultRaycastLayers`（= 除 `Ignore Raycast` 层外的所有层）。单位若在该层会被静默忽略（渲染正常但点不到）。改为显式 `Physics2D.AllLayers`，并加落空时 `OverlapCircleAll(point, 0.9)` 触控容错。
- **高亮环是「实心方块盖住角色」**：原实现是一个 `WhiteSprite` 方块、`sortingOrder 20`（高于单位 sprite 的 5/10）→ 一整块盖住角色，不像「选中」。改为**四边细杆方框**（`FrameHalf 0.8` / `FrameThick 0.1`），纯代码，不依赖素材。
- 另修两处「表现层无反馈」：
  - 世界单位文字（`TextMesh`）原先用默认 Arial（**无中文字形**）且 `MeshRenderer.sortingOrder = 0` **低于**单位 sprite（5/10）→ 名字被压住且中文不显示。改为 `tm.font = UiKit.Font`（CJK）+ `sortingOrder = 50`，名字/血量移到角色下方。
  - 轮盘按钮改**圆形**（`UiKit.CircleSprite` 纯代码生成 128×128 白圆），锚点从角色中心上移 105 到**头顶上方**，弧 140°→40°、半径 195、直径 190（移动端热区）。
- 布局可调：`BattleLayout`（`teamGap` / `arenaShift` / `baseOffset` / `frontY` / `backY`）挂在 `BattleController` 上作 `[SerializeField]`，**Play 中可在 Inspector 实时调**，调好抄回默认值即可 —— 不必手工搭场景。
- 诊断日志（定位用，确认后可关）：`[CQ.Cam]` 相机姿态；`[CQ.Click]` screen / world / overUi / hit / unit；`[CQ.Radial]` canvas 尺寸 / 世界锚点 / 画布局部锚点 / 每个按钮落点与可用性；`[CQ.Round]` 结束回合被触发（点开看堆栈确认调用方）。
- 素材接线：新增 `ArtLibrary`（`Resources/Arts/UI/` 按名加载，缺图回退纯色块）。单位示意图按配置 id 映射：sword→战士、mage→法师、guard→盾兵、grunt→小怪、claw→精英怪；敌人实例 id `grunt_0` 自动剥后缀。意图图标：attack/multi/charge→攻击、attackUp→增益、debuff→减益（defend→防御，暂无该意图类型）。
- **TMP**：用户已导入 TMP Essential Resources 并确认 5 张示意图 + 4 图标的 Import Settings 均为 `Sprite (2D and UI)`。TMP 迁移（`TextMeshProUGUI`）尚未做，仍用 legacy `Text` + `UiKit.Font`（SimHei）；迁移时 `CQ.Runtime.asmdef` 的 `references` 要加 `Unity.TextMeshPro`。

## 指向与数值反馈（阶段 24）
- **「守护」点友方被顶掉**：`OnUnitTapped` 的 `Team.Player` 分支原先无条件 `_selectedActor = unit.Id; _pendingSkill = null; ShowRadial()` —— 把「待选治疗目标」误当「切换选中角色」。修法：该分支先判 `_pendingSkill != null && NeedsSingleAlly(_pendingSkill)` → 直接 `TryChooseSkill(_selectedActor, _pendingSkill.id, unit.Id)` 并 `return`。
  - 通病：**点同一个物体可能有两种语义**（选角色 / 选目标），判定顺序必须以「是否有待选技能」优先。
- **「已登记但看不出结果」**：`ChooseSkill` 只登记计划，效果在 `EndRound()` 才结算（源码确认：`ChooseSkill` 写 `_plan`，`ActPlayer` → `SkillResolver.Resolve`）。故：
  - 登记时左栏行动条给该角色加〔技能名〕（`BattleController._queued`，`RefreshHud` 渲染）。
  - 结算后按血量快照差飘字（`SnapshotHp` + `FloatDeltas` → `FloatingText`，绿 `+N` / 红 `-N`）。
- **数值可见**：`UnitView` 纯代码数值条 —— 血条（双方，`BarW 1.5`，比例 + 左对齐，≤30% 转红）+ 能量条（青）/大招条（金，仅我方）。布局 y：血条 −0.95、能量 −1.19、大招 −1.32、名字/数值 −1.62、意图图标 1.34、意图文字 1.0、飘字 1.95。理由：只有 `HP/MAX` 文字看不出趋势，条才是回合制的可读性底线。
- **难度（数据，零硬编码）**：关卡 3 = 领主 + 2 爪牙。
  - 改前：敌总血 480、敌方每回合有效伤害约 75、我总血 340 + 治疗约 60 → 5 回合内崩。
  - 改后 `lord`：`maxHp 200`、`basic 22`、`aoe 12`、`charge 50`（仍 `interruptible`）；`claw`：`maxHp 60`、`slash 10`、`multi 4×3` → 敌总血 320、每回合约 55，先清爪牙可翻盘。
  - 关键前提：**打断是蓄力的唯一正解，而打断卡本期 UI 未接**，所以蓄力从 120 降到 50；卡牌接回后可再调硬。
  - `BalanceTests` 不受影响（只断言 level1 纯普攻可胜、各关 200 回合内终止、领主蓄力可打断）。
- **延后项（用户指定，不着急）**：数值表现还不够醒目 —— 血条与伤害数字偏小。强化方向见 `progress.md` 阶段 25（加粗加长血条、飘字放大延时、受击闪白/抖动、左栏逐条伤害日志），并可与 TMP 迁移（SDF 描边/阴影）一起做。

## TMP 迁移与数值表现（阶段 25）
### 缺字形警告的真根因：字体图集装不下（用户实测 \u2715 警告 → 阶段 25.1 修）
- 现象：Play 时刷 `The character with Unicode value \u2715 was not found in the [SourceHanSansHWSC-Bold SDF] font asset or any potential fallbacks. It was replaced by Unicode character \u25A1 in text object [Text].`
- 两个独立原因，都不是「字体没导入」：
  1. **符号确实不存在**：✕(U+2715, Dingbats) 不在思源黑体 cmap 里（解析 `.otf`：U+2715 → glyph 0）。技能按钮的「不可用」标记改用 ×(U+00D7)，该字符在字体里有。
  2. **图集容量不足（更隐蔽）**：资产图集 1024² 且 `m_IsMultiAtlasTexturesEnabled: 0`。pointSize 90 + padding 9 → 每个字形约 108×108 px，一张图集最多约 80 个 CJK 字形。而 demo 全部可显示文案（配置的角色名/技能名/敌人名/卡名/关卡名 + UI 文案 + 日志模板）统计下来有 **364 个不同汉字**（另有 5 个符号 U+00B1/U+00D7/U+2191/U+2192/U+2193）。图集用满后，之后出现的新汉字全部退化成 □ 并刷同一条警告；`TryAddCharacterInternal` 还会把它写进 `m_MissingUnicodesFromFontFile`，同进程内不再重试。
- 定位证据（一次临时 EditMode 诊断）：图集初始 `chars=115 / glyphs=103 / usedRects=used102/free77`；新增 关(U+5173)、并(U+5E76) 返回 false，而 卡/剑/士/第/回/合 返回 true（仅因它们已缓存）→ 无空位放新字形；同一份 `.otf` 的 cmap 里 U+5173 → glyph 10921（非 0）→ 字体有字形，**是容量问题**。
  - 教训：`HasCharacter(x, ..., tryAddCharacter: true)` 返回 false 有两种含义 —— 字体没有这个字，或图集放不下；别只看返回值，要看图集用量。
- 修法：两份 SDF 资产的 `m_AtlasWidth/Height` 1024→2048、自由字形矩形 1023→2047、`m_IsMultiAtlasTexturesEnabled` 0→1；`TmpKit.EnsureUsable` 运行时再兜一层开启多图集（防止资产被改回）。2048² 约 324 个 CJK 字形，超出时 TMP 自动再开一张图集（每张图集一份材质实例，TMP 内部 `TMP_MaterialManager` 处理，手建材质也兼容）。
- 回归防线：`FontAssetTests.All_Demo_Text_Fits_In_Font_Assets` 把**配置中全部名称 + UI 文案/日志模板**拼成一整串灌进两份字体资产，断言零缺字 —— 图集不够时它就会失败（正是它先抓出 U+5173），且不再依赖人们记住「别用 ✕」。
- 临时工具（在 `Logs/` 下，不入库）：`cmap_gid.py` 解析 `.otf` 的 cmap 报告码点→字形 id（0 = 缺失）；`char_budget.py` 统计工程内可显示字符总数并估算图集容量。

### 字体资产 material 为空 → 文字必不显示（本次真问题）
- 现象（静态审查即发现）：`Assets/Resources/Fonts/SourceHanSansHWSC-{Regular,Bold} SDF.asset` 两份资产的 `material: {fileID: 0}`（空），同目录也没有 `... Atlas Material.mat`。
- 后果：TMP 3.0.7 的 `TextMeshPro.LoadFontAsset()` 在 `m_sharedMaterial == null` 分支里，若 `m_fontAsset.material == null` 只打 `The Font Atlas Texture ... is missing.` 警告，接着仍执行 `m_sharedMaterial.SetFloat(...)` → 空引用、文字不可见。**这是「字体导入成功但一个字都不显示」的典型陷阱。**
- 修法（`Runtime/TmpKit.cs`）：装载时自愈 —— `material == null` 则补建 `TextMeshPro/Mobile/Distance Field` 材质，`_MainTex` 指向资产图集纹理（`atlasTexture`）、`_TextureWidth/Height`、`_GradientScale = atlasPadding + 1`。图集是 Dynamic 模式（`m_AtlasPopulationMode: 1`），运行时逐字栅格化，`Texture2D.Resize` 是原地改同一个对象，材质引用不会失效。
- 回归防线：`FontAssetTests`（4 条）覆盖「资产加载后 material 非空」「CJK 字形栅格化出 characterCount > 0」「uGUI 文本挂上 TMP 字体」。

### 世界空间 TMP 的定标方式
- 不手调 `fontSize`/`characterSize`（两者耦合、随字体资产 pointSize 变），改为 `MakeWorldText(..., targetHeight)` + `FitWorldHeight`：`ForceMeshUpdate()` 量测 `textBounds.size.y`，把 `localScale` 设为 `targetHeight / 量测高度`。
- 关键细节：**每次定标前把 localScale 重置为 1**，否则反复调用会连乘漂移；文字内容变化（血条 `9/9` → `120/120`）后要重新定标。
- 建文字时必须先给一段初值（空字符串量测高度 0 → 定标被跳过 → 文字保持默认巨大字号）。这是本次实现里最容易踩的坑。

### 数值表现（回应「血条与伤害数字不够醒目」）
- 血条：宽 1.5→1.9、底框高 0.42 / 填充 0.30，外描边 + 25/50/75% 刻度；≤30% 转红并脉冲（`Mathf.Abs(Sin)`）。
- 血条上直接写 `HP/MAX`（TMP 粗体 + 黑描边），名字移到条下、我方再补「能/大」一行 —— 只给比例看不出趋势，只给数字看不出剩余比例，两者都要。
- 飘字：字号随伤害量走（`MinH 0.46` ~ `MaxH 0.92` 世界高），弹入 0.6→1.18→1.0，时长 1.4s；受击闪白 + 横向抖动（`UnitView.Flash`），回血闪绿。
- 战报逐条日志：见下（Core 事件日志）。

### 结算事件日志（Core 新增，战报数据源）
- `Core/Battle/BattleEvent.cs`：`Kind`(Skill/Card/Intent) + `ActorId` + `ActionId`（技能 id / 卡 id / 意图 type）+ `TargetId` + `HpDelta` + `TargetDead`。
- **Core 只给机器 id，不给中文文案**：文案映射留在表现层（`BattleController.ActionLabel` / `IntentLabel`），保持 Core 语言无关、可测。
- 生成方式：`ActUnit` 里结算**前后血量快照差**，每个有变化的单位一条（AOE 一次动作 = N 条），无变化记 1 条 `HpDelta = 0`（增益/减益也可见）。
- 两个顺序陷阱：
  1. 动作描述必须在结算**前**取 —— 敌人意图游标在结算后前进，玩家的 `_plan` 结算后不再代表本次动作。
  2. 事件列表在 `EndRound` 开头清空（不是 `BeginRound`）：`EndRound` 末尾会调 `BeginRound`，若在那里清空，刚结算出来的事件会被自己抹掉。
- 表现层用游标（`_logCursor`）增量播报，引擎清空时游标归零，不重复、不丢。
- 回归测试 `BattleEventTests`（4 条）：逐单位事件血量合计 = 实际血量变化（防漏记/重记）、AOE = 3 条且目标互不相同、敌方事件带意图 type 与锁定目标、跨回合不堆积。

### 离线快速验证回路（本会话新增，非交付物）
- Unity 编辑器开着时工程被锁，无法 batchmode；等 Unity 跑一轮太慢。故在 `Temp/CoreCheck/`（gitignore 内）建了一个只跑纯 Core 测试的临时工程：
  - `dotnet build CQ.Runtime.csproj` 能编过刚改的 Runtime 代码（Unity 生成的 csproj + 手补 `Unity.TextMeshPro` 引用与新文件条目；csproj 本身被 gitignore，Unity 会重新生成覆盖）。
  - 纯 Core 的 49 个测试离线跑通。
- 坑：Unity 自带 `nunit.framework.dll`（net35）的 `Assert` 依赖 `System.Runtime.Remoting.Messaging.CallContext`，在 .NET Core 下 `TypeLoadException`，不能直接引；改用最小断言替身（只实现 `AreEqual/IsTrue/IsFalse/IsNotNull/IsNotEmpty/Greater`）。
- 坑：`Unity.exe -batchmode` 启动时会**删掉工程 Temp 目录**（正常清理），所以 `-logFile` / `-testResults` 不要写进 `Temp/`，否则日志随启动一起没。
- 坑：`-runTests` 不能和 `-quit` 同用 —— 带 `-quit` 时 Unity 导入完就退出（日志 `Batchmode quit successfully invoked`），一条测试都不跑、也不产出结果 XML。去掉 `-quit`，测试跑完自己退出。
- 坑：受限沙箱下 Unity batchmode 报 `Connection to channel LicenseClient-... refused` → `Timed-out after 60.00s` → 返回码 199（许可证客户端走本地 IPC 命名管道，被沙箱挡）。放宽沙箱后正常。

### TMP Dynamic 资产会把字形缓存写回资产文件（勿提交）
- 编辑器里跑一次用到中文的测试/Play 后，两份 SDF 资产从 ~4.7KB 涨到 ~2.1MB（图集纹理序列化成 1024×1024 的十六进制数据 + `m_GlyphTable`/`m_CharacterTable`）。
- 危险点：`image data: 0` —— 像素并没有真正写进资产（只有全零占位数据），但字形表/图集矩形已经记下来了。这种状态一旦提交，运行时会认为字形已在图集里而**跳过重新栅格化** → 中文可能整片不显示。
- 处置：每次测试后 `git checkout -- "Assets/Resources/Fonts/*SDF.asset"` 还原成空图集的 pristine 状态；Dynamic 模式本来就会在运行时逐字栅格化（已验证：从 pristine 资产起跑，`FontAssetTests` 的材料自愈 + 中文栅格化全过）。

## uGUI 交互重做（阶段 20）
- 动机：原全 IMGUI（OnGUI）热区小、难做轮盘、高 DPI 不缩放，真机点选不便、不可录屏。用户定调：点角色选中 → 角色周围弧形技能 → 点目标索敌。
- 决策（用户确认）：迁 uGUI 全部重做；本期只做技能轮盘（卡牌交互后置）；技能摆角色周围弧形。
- 落地：Canvas Screen Space Overlay + CanvasScaler(1920×1080, match 0.5) + EventSystem + StandaloneInputModule，全代码构建零场景资产（规避「场景未保存」复发）。
- 输入：世界单位加 `BoxCollider2D`，`Physics2D.OverlapPoint` 点选；`EventSystem.IsPointerOverGameObject` 挡 UI 点击误触单位。
- 状态机：点我方角色（黄环）→ 弧形 3 技能按钮（不可用置灰）→ 单体伤害点敌 / 单体治疗点友方 / 自愈或全体立即出招。
- 手牌：`DrawHand` 已随 OnGUI 移除，卡牌交互后置；`BattleEngine.PlayCard` 保留但本期 UI 不接。
- 字体：uGUI 中文曾用 `Assets/Resources/Fonts/SimHei.ttf`（9.3MB，SimHei 属 Windows 附带字体，仅临时演示用）。**阶段 25 已删除**，改思源黑体 HW SC SDF（OFL 可商用；源 `.otf` 被 SDF 资产引用，仍在构建内）。

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

## 素材盘点（阶段 21，明早接线）
- 字体：`Assets/Resources/Fonts/SourceHanSansHWSC-Regular SDF.asset` + `Bold SDF.asset`（TMP Font Asset，Font Asset Creator 生成）。思源黑体 HW SC，OFL 许可可商用。**阶段 25 已完成迁移**：`UiKit.Font/FontBold` 加载 `TMP_FontAsset`，UI 文本为 `TextMeshProUGUI`，世界文字为 `TextMeshPro`（3D），legacy `Text`/`TextMesh`/SimHei 全部移除。资产缺 material 的问题见上文「字体资产 material 为空」。
- 示意图 5 张（`Assets/Resources/Arts/UI/`）：战士/法师/盾兵/小怪/精英怪 ↔ sword/mage/guard/grunt/claw。缺 lord（领主/Boss），暂复用精英怪或后补。
- 意图图标 4：攻击/防御/增益/减益；选中框 `按钮选中框.png`（可复用为选中高亮环）。
- 缺（后置）：技能图标 9 个（轮盘按钮文字占位）、面板/按钮 9-slice 描边底。
- 场景背景：暂缓，用 U3D 原生纯色（用户定，不影响交互验收）。

## 素材接线收尾（阶段 25.2）
- `按钮选中框.png` 实测是**空心白色圆环**（200×200，环宽约 11px、外径约 184px、近白），不是矩形 9-slice 底框 → 不做面板拉伸，改当「脚下选中圆环」用：`UnitView.SetHighlight` 选中(黄)/可目标(青)时在脚下显环（`RingY=-0.78`、直径 1.9 世界单位）；原代码四边方框留作素材缺失兜底。
- 技能图标 9 个仍未到位 → 占位：伤害技能用「攻击」图标、治疗/自愈用「增益」图标（`ArtLibrary.SkillIcon`）；真图标到位只改这一个映射，`UiKit.MakeCircleButton` 已支持 `icon` 参数（图标居上、文字下沉）。
- 诊断日志关闭：`BattleController.verboseClick`、`RadialSkillMenu.verbose` 默认 false（点选打空/坐标错位时再打开）。
- 回归：`ArtLibraryTests` 3 条（单位示意图含 `grunt_0`/`lord_2` 剥后缀、意图图标全 type、圆环 + 技能占位图标），全量 EditMode 78/78 绿、缺字形警告 0。
- 仍缺（素材层）：lord 真示意图、9 个真技能图标、面板/按钮 9-slice 底。

## 轮盘改法决策（用户确认）
- 弃扇形按钮（对非专业美术过重），改 **3 个圆形可交互按钮**，代码定位在「选中角色上方」沿弧分布。
- 落地要点（`RadialSkillMenu`）：按钮方块→圆形、锚点从角色中心→角色头顶上方、保留弧状排布；技能图标文字占位，后补图标不改结构。

## 资源
- `demo任务清单.md`（需求源头）
- `战斗设计.md`（战斗规则源头）
- `Assets/Settings/Renderer2D.asset`（2D 渲染）

---

*每执行 2 次查看/浏览器/搜索操作后更新此文件。*