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
    }
}