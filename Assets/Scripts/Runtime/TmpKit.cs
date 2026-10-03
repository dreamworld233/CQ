using TMPro;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// TMP 字体资产装载与自愈。
    /// 工程内两份 SDF 资产（思源黑体 HW SC Regular/Bold）由 Font Asset Creator 生成，
    /// 但资产里 material 为 null —— TMP 3.0.7 在 material 为空时只打一条
    /// “The Font Atlas Texture ... is missing.” 警告，随后对 null 材质赋值会抛异常、文字不可见。
    /// 故这里按需补一个 Mobile/Distance Field 材质：MainTex 指向该资产的图集纹理，
    /// 图集为 Dynamic 模式，运行时逐字栅格化（Resize 原地改同一个 Texture2D，材质引用不失效）。
    /// </summary>
    public static class TmpKit
    {
        private const string ShaderName = "TextMeshPro/Mobile/Distance Field";

        private static TMP_FontAsset _regular;
        private static TMP_FontAsset _bold;

        /// <summary>正文字体（缺资产时回退 TMP Settings 默认字体）。</summary>
        public static TMP_FontAsset Regular => _regular != null ? _regular : (_regular = Load("Fonts/SourceHanSansHWSC-Regular SDF"));

        /// <summary>粗体字体（数值/飘字用）。缺资产时回退正文。</summary>
        public static TMP_FontAsset Bold => _bold != null ? _bold : (_bold = Load("Fonts/SourceHanSansHWSC-Bold SDF") ?? Regular);

        private static TMP_FontAsset Load(string resourcePath)
        {
            var fa = Resources.Load<TMP_FontAsset>(resourcePath);
            if (fa == null)
            {
                Debug.LogWarning("[CQ.Tmp] 未找到字体资产 Resources/" + resourcePath);
                return null;
            }
            EnsureUsable(fa);
            return fa;
        }

        /// <summary>缺材质则补一个；顺带保证字形查找表已读。</summary>
        public static void EnsureUsable(TMP_FontAsset fa)
        {
            if (fa == null) return;
            if (fa.characterLookupTable == null || fa.characterLookupTable.Count == 0)
                fa.ReadFontAssetDefinition();
            if (fa.material != null) return;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[CQ.Tmp] 找不到 shader " + ShaderName + "，无法修复字体材质");
                return;
            }

            var mat = new Material(shader) { name = fa.name + " Atlas Material (runtime)" };
            mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            mat.SetFloat(ShaderUtilities.ID_TextureWidth, fa.atlasWidth);
            mat.SetFloat(ShaderUtilities.ID_TextureHeight, fa.atlasHeight);
            mat.SetFloat(ShaderUtilities.ID_GradientScale, fa.atlasPadding + 1);
            mat.SetFloat(ShaderUtilities.ID_WeightNormal, fa.normalStyle);
            mat.SetFloat(ShaderUtilities.ID_WeightBold, fa.boldStyle);
            mat.SetFloat("_CullMode", 0);
            fa.material = mat;
            Debug.Log("[CQ.Tmp] 已为 " + fa.name + " 补建运行时材质（资产内 material 为空）");
        }

        /// <summary>
        /// 世界空间文字：建 RectTransform 物体 + TextMeshPro（3D）+ 按目标世界高度定缩放。
        /// 用「量测后缩放」而不是手调字号/字符尺寸，改布局只需给目标高度。
        /// </summary>
        public static TextMeshPro MakeWorldText(Transform parent, string goName, Vector3 localPos,
            TMP_FontAsset font, float targetHeight, Color color, int sortingOrder,
            FontStyles style = FontStyles.Normal, string text = null)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.localPosition = localPos;
            rt.sizeDelta = new Vector2(8f, 2f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
            tmp.fontSize = 48f;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (!string.IsNullOrEmpty(text)) tmp.text = text;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = sortingOrder;   // 与 SpriteRenderer 同在 Default 排序层，靠 order 压住单位图
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            FitWorldHeight(tmp, targetHeight);
            return tmp;
        }

        /// <summary>
        /// 把文字量测高度缩放到目标世界高度。每次都从缩放 1 重新量测，故可反复调用不累积漂移
        /// （文字内容变化后需重调，例如血条数字从 9/9 变 120/120）。
        /// </summary>
        public static void FitWorldHeight(TextMeshPro tmp, float targetHeight)
        {
            if (tmp == null || targetHeight <= 0f) return;
            var t = tmp.transform;
            t.localScale = Vector3.one;
            if (string.IsNullOrEmpty(tmp.text)) return;
            tmp.ForceMeshUpdate();
            float h = tmp.textBounds.size.y;
            if (h <= 0.0001f) return;
            t.localScale = Vector3.one * (targetHeight / h);
        }

        /// <summary>给 SDF 文字加描边（材质实例由 TMP 惰性创建）。2D/3D 文本通用。</summary>
        public static void SetOutline(TMP_Text tmp, Color color, float width)
        {
            if (tmp == null) return;
            var mat = tmp.fontMaterial;
            if (mat == null || !mat.HasProperty(ShaderUtilities.ID_OutlineWidth)) return;
            mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        }
    }
}
