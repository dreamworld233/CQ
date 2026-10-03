using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
}
