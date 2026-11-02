using System;
using System.IO;
using System.Text;
using CQ.Core.Persistence;
using UnityEngine;

namespace CQ.Runtime.Persistence
{
    public enum SaveStatus { Empty, Ready, Recovered, Invalid, Unsupported, IoError }

    public sealed class SaveRead<T> where T : class
    {
        public SaveStatus Status { get; }
        public T Data { get; }
        public string Message { get; }
        public bool CanLoad => Data != null && (Status == SaveStatus.Ready || Status == SaveStatus.Recovered);
        public SaveRead(SaveStatus status, T data = null, string message = null)
        { Status = status; Data = data; Message = message; }
    }

    /// <summary>固定槽位；同目录临时文件+原子替换，保留上一份有效数据。调用者必须确认覆盖/删除。</summary>
    public sealed class LocalSaveStore
    {
        public const int SlotCount = 3;
        private const long MaxBytes = 1024 * 1024;
        public string Root { get; }

        public LocalSaveStore(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException(nameof(root));
            Root = Path.GetFullPath(root);
        }

        public string SlotPath(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(Root, "slot-" + (slot + 1) + ".json");
        }

        public SaveRead<GameSave> ReadSlot(int slot) => Read(SlotPath(slot), GameSave.Signature,
            GameSave.CurrentVersion, (GameSave data) => data.Validate());

        public SaveRead<UserSettings> ReadSettings() => Read(Path.Combine(Root, "settings.json"), UserSettings.Signature,
            1, (UserSettings data) => data.Validate());

        public bool WriteSlot(int slot, GameSave data, bool replace, out string error)
        {
            error = data?.Validate() ?? (data == null ? "存档为空" : null);
            if (error != null) return false;
            var previous = ReadSlot(slot);
            if (!replace && !previous.CanLoad && previous.Status != SaveStatus.Empty)
            { error = "原存档不可写，请返回菜单确认覆盖或删除"; return false; }
            var path = SlotPath(slot);
            if (replace)
            {
                // 清理失败必须发生在替换主档之前，避免“新主档+旧旅程备份”的混合状态。
                try { if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
                catch (Exception ex) when (IsFileError(ex))
                { error = "旧备份无法清理，新档未写入：" + ex.Message; return false; }
            }
            return Write(path, JsonUtility.ToJson(data, true), replace || previous.Status != SaveStatus.Ready, out error);
        }

        public bool WriteSettings(UserSettings data, out string error)
        {
            error = data?.Validate() ?? (data == null ? "设置为空" : null);
            return error == null && Write(Path.Combine(Root, "settings.json"), JsonUtility.ToJson(data, true),
                ReadSettings().Status != SaveStatus.Ready, out error);
        }

        public bool DeleteSlot(int slot, out string error)
        {
            error = null;
            try
            {
                var path = SlotPath(slot);
                // 先删除备份，避免删除主档后再次被备份恢复。
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception ex) when (IsFileError(ex)) { error = "删除失败：" + ex.Message; return false; }
        }

        [Serializable]
        private sealed class Header { public string signature; public int version; }

        private static SaveRead<T> ReadOne<T>(string path, string signature, int version, Func<T, string> validate) where T : class
        {
            try
            {
                if (!File.Exists(path)) return new SaveRead<T>(SaveStatus.Empty);
                if (new FileInfo(path).Length > MaxBytes) return new SaveRead<T>(SaveStatus.Invalid, message: "文件过大");
                string json = File.ReadAllText(path, Encoding.UTF8);
                var header = JsonUtility.FromJson<Header>(json);
                if (header != null && header.signature == signature && header.version > version)
                    return new SaveRead<T>(SaveStatus.Unsupported, message: "由更高版本创建，当前版本不能加载");
                var data = JsonUtility.FromJson<T>(json);
                string error = data == null ? "文件内容为空" : validate(data);
                return error == null ? new SaveRead<T>(SaveStatus.Ready, data) : new SaveRead<T>(SaveStatus.Invalid, message: error);
            }
            catch (ArgumentException) { return new SaveRead<T>(SaveStatus.Invalid, message: "文件内容损坏"); }
            catch (Exception ex) when (IsFileError(ex)) { return new SaveRead<T>(SaveStatus.IoError, message: "读取失败：" + ex.Message); }
        }

        private static SaveRead<T> Read<T>(string path, string signature, int version, Func<T, string> validate) where T : class
        {
            var primary = ReadOne(path, signature, version, validate);
            if (primary.CanLoad || primary.Status == SaveStatus.Unsupported || primary.Status == SaveStatus.IoError) return primary;
            var backup = ReadOne(path + ".bak", signature, version, validate);
            if (backup.CanLoad) return new SaveRead<T>(SaveStatus.Recovered, backup.Data, "主档缺失或损坏，将使用上一次备份");
            if (primary.Status == SaveStatus.Empty && backup.Status != SaveStatus.Empty) return backup;
            return primary;
        }

        private bool Write(string path, string json, bool preserveBackup, out string error)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            error = null;
            try
            {
                Directory.CreateDirectory(Root);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, preserveBackup ? null : path + ".bak");
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception ex) when (IsFileError(ex)) { error = "保存失败：" + ex.Message; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) when (IsFileError(ex)) { Debug.LogWarning("临时存档清理失败：" + ex.Message); }
            }
        }

        private static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
            ex is System.Security.SecurityException || ex is NotSupportedException;
    }
}
