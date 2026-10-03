using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CQ.Editor
{
    /// <summary>
    /// Android 构建配置（一次性）：包名 + 锁定横屏 + ARMv7/ARM64 + IL2CPP。
    /// 菜单 CQ/配置 Android PlayerSettings 执行一次即可，之后能正常 Build。
    /// </summary>
    public static class AndroidBuildMenu
    {
        [MenuItem("CQ/配置 Android PlayerSettings (横屏+ARM64+IL2CPP)")]
        public static void ConfigureAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.dreamworld233.cq");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            Debug.Log("[CQ.Editor] Android 已配置：横屏左 + ARMv7/ARM64 + IL2CPP + com.dreamworld233.cq");
        }
    }
}