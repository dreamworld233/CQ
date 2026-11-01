using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace CQ.Editor
{
    /// <summary>独立于临时战斗素材的初期应用图标；替换PNG后可重复应用。</summary>
    public static class AppIconSettings
    {
        private const string Root = "Assets/Art/AppIcon/";

        [MenuItem("CQ/配置初期应用图标")]
        public static void Configure()
        {
            var icon = Load("AppIcon.png", false);
            var foreground = Load("AppIconForeground.png", true);
            var background = Load("AppIconBackground.png", false);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            SetAndroid(AndroidPlatformIconKind.Legacy, icon);
            SetAndroid(AndroidPlatformIconKind.Round, icon);
            // Unity Android adaptive层顺序：0前景（透明），1背景（满幅不透明）。
            SetAndroid(AndroidPlatformIconKind.Adaptive, foreground, background);
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("[CQ.Icon] 已配置默认及Android legacy/round/adaptive图标，未修改其他PlayerSettings。");
        }

        public static void Verify()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "AppIcon.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "AppIconForeground.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "AppIconBackground.png");
            var defaults = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (icon == null || defaults.Length == 0 || defaults.Any(texture => texture != icon))
                throw new InvalidOperationException("默认图标未正确配置。");
            VerifyAndroid(AndroidPlatformIconKind.Legacy, icon);
            VerifyAndroid(AndroidPlatformIconKind.Round, icon);
            VerifyAndroid(AndroidPlatformIconKind.Adaptive, foreground, background);
        }

        private static Texture2D Load(string name, bool transparent)
        {
            string path = Root + name;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少图标资源：" + path);
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = transparent;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != 1024 || texture.height != 1024)
                throw new InvalidOperationException("图标应为1024×1024：" + path);
            return texture;
        }

        private static void SetAndroid(PlatformIconKind kind, params Texture2D[] textures)
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots.Length == 0) throw new InvalidOperationException("Android图标槽位不可用，请安装Android Build Support。");
            foreach (var slot in slots) slot.SetTextures(textures);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
        }

        private static void VerifyAndroid(PlatformIconKind kind, params Texture2D[] textures)
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots.Length == 0) throw new InvalidOperationException("Android图标槽位缺失。");
            foreach (var slot in slots)
                for (int layer = 0; layer < textures.Length; layer++)
                    if (textures[layer] == null || slot.GetTexture(layer) != textures[layer])
                        throw new InvalidOperationException("Android图标层未正确配置：" + kind + "/" + layer);
            Debug.Log("[CQ.Icon] 验证 " + kind + "：" + slots.Length + "个尺寸、" + textures.Length + "层。");
        }
    }
}
