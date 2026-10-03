using System;
using System.IO;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// JsonUtility 加载器。JSON 反序列化需要 UnityEngine.JsonUtility，
    /// 故放 Runtime 层（Core 层零 UnityEngine 依赖，只持有纯 DTO）。
    /// </summary>
    public static class ConfigJson
    {
        /// <summary>
        /// 文件系统加载（PC 编辑器 / EditMode 测试用）。Android 上 streamingAssetsPath
        /// 在 APK 内（jar:），System.IO 读不到，故真机不走此路径。
        /// </summary>
        public static LoadResult<T> LoadAll<T>(string directory, Func<T, string> validate) where T : class
        {
            var result = new LoadResult<T>();
            if (!Directory.Exists(directory))
            {
                result.errors.Add("目录不存在: " + directory);
                return result;
            }

            var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var path in files)
            {
                var name = Path.GetFileName(path);
                try
                {
                    var json = File.ReadAllText(path);
                    var item = JsonUtility.FromJson<T>(json);
                    var error = validate != null ? validate(item) : null;
                    if (string.IsNullOrEmpty(error))
                        result.items.Add(item);
                    else
                        result.errors.Add(name + ": " + error);
                }
                catch (Exception e)
                {
                    result.errors.Add(name + ": " + e.Message);
                }
            }
            return result;
        }

        /// <summary>
        /// Resources 同步加载（跨平台安全，Android 真机可用）。JSON 位于任一 Resources
        /// 目录下，按文件名 + 扩展名作为 TextAsset 打进包，`Resources.LoadAll` 在真机同步读得到。
        /// </summary>
        public static LoadResult<T> LoadAllFromResources<T>(string resourceFolder, Func<T, string> validate) where T : class
        {
            var result = new LoadResult<T>();
            var textAssets = Resources.LoadAll<TextAsset>(resourceFolder);
            foreach (var ta in textAssets)
            {
                try
                {
                    var item = JsonUtility.FromJson<T>(ta.text);
                    var error = validate != null ? validate(item) : null;
                    if (string.IsNullOrEmpty(error))
                        result.items.Add(item);
                    else
                        result.errors.Add(ta.name + ": " + error);
                }
                catch (Exception e)
                {
                    result.errors.Add(ta.name + ": " + e.Message);
                }
            }
            return result;
        }
    }
}