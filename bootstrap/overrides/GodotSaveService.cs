using System;
using System.IO;
using System.Text.Json;
using Godot;
using LordWar.Save;
using LordWar.Simulation;

namespace LordWar.GodotRuntime {
    /// <summary>Replaces Unity JsonUtility/Application.persistentDataPath with Godot user:// + System.Text.Json.</summary>
    public static class GodotSaveService {
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            IncludeFields = true,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        static string SavePath => ProjectSettings.GlobalizePath("user://lordwar_save.json");
        static string BackupPath => SavePath + ".bak";
        static string TempPath => SavePath + ".tmp";

        public static bool Save(GameWorld world, out string message) {
            message = "";
            if (world == null) { message = "世界为空"; return false; }
            try {
                GameSave data = GameSaveService.Capture(world);
                string error;
                if (!GameSaveService.Validate(data, out error)) { message = "存档前校验失败：" + error; return false; }

                Directory.CreateDirectory(Path.GetDirectoryName(SavePath) ?? ".");
                File.WriteAllText(TempPath, JsonSerializer.Serialize(data, JsonOptions), System.Text.Encoding.UTF8);
                GameSave verify = JsonSerializer.Deserialize<GameSave>(File.ReadAllText(TempPath, System.Text.Encoding.UTF8), JsonOptions);
                if (!GameSaveService.Validate(verify, out error)) { message = "存档回读校验失败：" + error; return false; }

                if (File.Exists(SavePath)) {
                    File.Copy(SavePath, BackupPath, true);
                    File.Move(TempPath, SavePath, true);
                } else {
                    File.Move(TempPath, SavePath);
                }
                message = "存档成功";
                return true;
            } catch (Exception ex) {
                GD.PushError("保存失败：" + ex);
                message = "保存失败：" + ex.Message;
                return false;
            }
        }

        public static bool TryRead(out GameSave save, out string message) {
            save = null;
            message = "";
            string[] candidates = { SavePath, BackupPath };
            foreach (string path in candidates) {
                if (!File.Exists(path)) continue;
                try {
                    GameSave data = JsonSerializer.Deserialize<GameSave>(File.ReadAllText(path, System.Text.Encoding.UTF8), JsonOptions);
                    string error;
                    if (!GameSaveService.Validate(data, out error)) { GD.PushWarning("存档不可用：" + error); continue; }
                    save = data;
                    message = "读档可用";
                    return true;
                } catch (Exception ex) {
                    GD.PushWarning("读取存档失败：" + ex.Message);
                }
            }
            message = "没有可用存档";
            return false;
        }
        public static bool Load(GameWorld world, out string message) {
            if (world == null) { message = "世界为空"; return false; }
            GameSave data;
            if (!TryRead(out data, out message)) return false;
            try { GameSaveService.Restore(world, data); message = "读档成功"; return true; }
            catch (Exception ex) { message = "读档失败：" + ex.Message; GD.PushError(message); return false; }
        }
    }
}
