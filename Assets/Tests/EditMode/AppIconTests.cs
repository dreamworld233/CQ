using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

public class AppIconTests
{
    private const string Root = "Assets/Art/AppIcon/";

    [TestCase("AppIcon.png", false)]
    [TestCase("AppIconBackground.png", false)]
    [TestCase("AppIconForeground.png", true)]
    public void TexturesImportAtFullResolutionWithoutLossyCompression(string name, bool transparent)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + name);
        Assert.IsNotNull(texture);
        Assert.AreEqual(1024, texture.width);
        Assert.AreEqual(1024, texture.height);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + name);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        Assert.AreEqual(transparent, importer.alphaIsTransparency);
    }

    [Test]
    public void DefaultAndAllAndroidSlotsReferenceOwnedIconTextures()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "AppIcon.png");
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "AppIconForeground.png");
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "AppIconBackground.png");
        var defaults = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        Assert.IsNotEmpty(defaults);
        Assert.IsTrue(defaults.All(texture => texture == icon));
        CheckSlots(AndroidPlatformIconKind.Legacy, icon);
        CheckSlots(AndroidPlatformIconKind.Round, icon);
        CheckSlots(AndroidPlatformIconKind.Adaptive, foreground, background);
    }

    [Test]
    public void AdaptiveForegroundIsTransparentAndFitsCircularSafeArea_BackgroundIsOpaque()
    {
        var foreground = new Texture2D(2, 2);
        var background = new Texture2D(2, 2);
        try
        {
            Assert.IsTrue(foreground.LoadImage(File.ReadAllBytes(Root + "AppIconForeground.png")));
            Assert.IsTrue(background.LoadImage(File.ReadAllBytes(Root + "AppIconBackground.png")));
            var pixels = foreground.GetPixels32();
            int visible = 0;
            float radius = 1024f * 33f / 108f;
            for (int y = 0; y < 1024; y++)
                for (int x = 0; x < 1024; x++)
                {
                    if (pixels[y * 1024 + x].a < 16) continue;
                    visible++;
                    float dx = x + 0.5f - 512f;
                    float dy = y + 0.5f - 512f;
                    Assert.LessOrEqual(dx * dx + dy * dy, radius * radius, "前景不可被圆形自适应遮罩裁切");
                }
            Assert.Greater(visible, 100000);
            Assert.Less(visible, 350000);
            Assert.AreEqual(0, pixels[0].a);
            Assert.IsTrue(background.GetPixels32().All(pixel => pixel.a == 255));
        }
        finally
        {
            Object.DestroyImmediate(foreground);
            Object.DestroyImmediate(background);
        }
    }

    private static void CheckSlots(PlatformIconKind kind, params Texture2D[] textures)
    {
        var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        Assert.AreEqual(6, slots.Length, "Android六种密度都应有图标");
        foreach (var slot in slots)
            for (int layer = 0; layer < textures.Length; layer++)
                Assert.AreSame(textures[layer], slot.GetTexture(layer));
    }
}
