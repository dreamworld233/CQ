using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CQ.Runtime;

/// <summary>
/// TMP 字体接线验证：资产材质自愈 + CJK 字形栅格化 + 世界文字按目标高度定标。
/// 这几条在 Play 之前就能跑出来，专门用来拦住「字体材质为空 → 文字全不显示」这类问题。
/// </summary>
public class FontAssetTests
{
    private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void Cleanup()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Font_Assets_Load_With_Usable_Material()
    {
        var regular = CQ.Runtime.TmpKit.Regular;
        Assert.IsNotNull(regular, "Resources/Fonts 下应有思源黑体 SDF 资产");
        Assert.IsNotNull(regular.material, "材质为空时 TMP 会给 null 材质赋值并报错，TmpKit 应已补建");
        Assert.IsNotNull(regular.atlasTexture, "SDF 资产应带图集纹理（Dynamic 模式运行时扩容）");

        var bold = CQ.Runtime.TmpKit.Bold;
        Assert.IsNotNull(bold, "粗体字体应可用（缺资产时回退正文）");
        Assert.IsNotNull(bold.material);
    }

    [Test]
    public void World_Text_Rasterizes_Cjk_And_Matches_Target_Height()
    {
        const float target = 0.3f;
        var tmp = CQ.Runtime.TmpKit.MakeWorldText(null, "HpText", Vector3.zero,
            CQ.Runtime.UiKit.FontBold, target, Color.white, 55, FontStyles.Normal, "12/40");
        _spawned.Add(tmp.gameObject);

        tmp.ForceMeshUpdate();

        Assert.Greater(tmp.textInfo.characterCount, 0, "世界文字应栅格化出字形");
        float h1 = WorldHeight(tmp);
        Assert.Greater(h1, 0f);
        // TMP 的 textBounds 是文字局部空间（不受 transform 缩放影响），故世界高度必须读渲染器 bounds。
        // 容差放宽：网格包围盒可能含少量边距。
        Assert.Greater(h1, target * 0.6f, "世界高度应接近目标高度（±40%）");
        Assert.Less(h1, target * 1.4f, "世界高度应接近目标高度（±40%）");

        // 内容变长后重新定标（血条数字位数会变），高度须保持同一目标
        tmp.text = "120/120";
        CQ.Runtime.TmpKit.FitWorldHeight(tmp, target);
        float h2 = WorldHeight(tmp);
        Assert.Greater(h2, h1 * 0.85f, "改文字后重新定标应仍贴合目标高度");
        Assert.Less(h2, h1 * 1.15f, "改文字后重新定标应仍贴合目标高度");
    }

    private static float WorldHeight(TextMeshPro tmp)
    {
        var r = tmp.GetComponent<Renderer>();
        return r != null ? r.bounds.size.y : 0f;
    }

    [Test]
    public void World_Text_Renders_Chinese_Characters()
    {
        var tmp = CQ.Runtime.TmpKit.MakeWorldText(null, "Label", Vector3.zero,
            CQ.Runtime.UiKit.Font, 0.28f, Color.white, 55, FontStyles.Normal, "剑士");
        _spawned.Add(tmp.gameObject);

        tmp.ForceMeshUpdate();

        Assert.AreEqual(2, tmp.textInfo.characterCount, "中文字符应能栅格化（思源黑体 Dynamic 图集）");
    }

    [Test]
    public void Ugui_Text_Uses_Tmp_Font()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _spawned.Add(canvasGo);

        var text = CQ.Runtime.UiKit.MakeText(canvasGo.transform, "结束回合", 32, Color.white, TextAnchor.MiddleCenter, true);
        text.ForceMeshUpdate();

        Assert.IsNotNull(text.font, "uGUI 文本应挂 TMP 字体资产");
        Assert.Greater(text.textInfo.characterCount, 0, "uGUI 文本应栅格化出字形");
    }

    /// <summary>
    /// demo 全部可能显示的文案都必须有字形，否则 TMP 会退化成 □ 并刷
    /// 「The character with Unicode value \uXXXX was not found ...」警告。
    /// 两个踩过的坑：(1) 技能按钮不可用标记原用 ✕(U+2715)，思源黑体没有该字形；
    /// (2) 资产图集原为 1024² 且未开多图集，pointSize 90 / padding 9 下约 80 个 CJK 字形就满，
    ///     后半段出现的汉字全部变成 □ —— 本用例把配置与文化字全量灌进去，超容即失败。
    /// </summary>
    [Test]
    public void All_Demo_Text_Fits_In_Font_Assets()
    {
        var samples = new List<string>
        {
            // UI 文案与符号（含符文分隔、箭头、坐标标记）
            "行动条", "第 1 回合", "出手顺序", "战报", "结束回合", "重开", "下一关 → ", "我方胜利", "我方失败",
            "重试加载", "配置错误，查看 Console", "无可用关卡", "请点目标", "此技能点友方", "出招失败（能量/目标不符）",
            "已登记，结束回合结算", "锁定 ", "开战 ", "×", "→", "↑", "↓", "〔〕",
            // 战斗文本模板
            "能 3/3  大 0/3", "12/40", "120/120", "攻击", "连击", "蓄力", "攻增", "减益", "全体", "卡", "倒", "亡",
            "我 剑士〔斩击〕 → 小怪 -12", "卡〔打断〕 → 领主"
        };

        var chars = ConfigLoader.LoadCharacters();
        foreach (var c in chars.items)
        {
            samples.Add(c.name);
            if (c.skills == null) continue;
            foreach (var s in c.skills) samples.Add(s.name);
        }

        var enemies = ConfigLoader.LoadEnemies();
        foreach (var e in enemies.items) samples.Add(e.name);

        var cards = ConfigLoader.LoadCards();
        foreach (var c in cards.items) samples.Add(c.name);

        var levels = ConfigLoader.LoadLevels();
        foreach (var l in levels.items) samples.Add(l.name);

        string all = string.Join("", samples);
        Assert.IsNotEmpty(all);

        foreach (var fa in new[] { CQ.Runtime.TmpKit.Regular, CQ.Runtime.TmpKit.Bold })
        {
            uint[] missing;
            bool ok = fa.HasCharacters(all, out missing, true, true);
            string detail = (missing == null || missing.Length == 0)
                ? ""
                : string.Join(",", System.Array.ConvertAll(missing, v => "U+" + v.ToString("X4")));
            Assert.IsTrue(ok, fa.name + " 缺 " + (missing != null ? missing.Length : 0) + " 个字形：" + detail);
        }
    }
}
