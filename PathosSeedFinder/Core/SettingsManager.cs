using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PathosSeedFinder.Core
{
    public class AppSettings
    {
        public string PathosDirectory { get; set; } = "";
        public string Tileset { get; set; } = "sonyvanda";
        public string Language { get; set; } = "zh";
    }

    public static class SettingsManager
    {
        private static readonly string ConfigFileName = "config.json";

        public static string GetConfigFilePath()
        {
            // Prefer current working directory or base directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localConfig = Path.Combine(baseDir, ConfigFileName);
            if (File.Exists(localConfig)) return localConfig;

            // Check parent source directory if running in dev (bin/Debug/net8.0-windows)
            string srcConfig = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..", ConfigFileName));
            if (File.Exists(srcConfig)) return srcConfig;

            return localConfig;
        }

        public static AppSettings Load()
        {
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.PathosDirectory))
                    {
                        return settings;
                    }
                }
                catch { }
            }

            // Fallback: Try auto-detecting
            var detected = FindCandidates();
            if (detected.Count > 0)
            {
                var s = new AppSettings { PathosDirectory = detected[0] };
                Save(s);
                return s;
            }

            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            string path = GetConfigFilePath();
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法保存设置到 {path}: {ex.Message}");
            }
        }

        public static bool ValidatePathosDirectory(string dir, out string error)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                error = "游戏路径不能为空！";
                return false;
            }

            if (!Directory.Exists(dir))
            {
                error = $"指定的目录不存在: {dir}";
                return false;
            }

            string engineDll = Path.Combine(dir, "PathosEngine.dll");
            if (!File.Exists(engineDll))
            {
                error = "未能找到核心文件 PathosEngine.dll，请确保选择的是 Pathos 游戏的安装根目录。";
                return false;
            }

            string officialDll = Path.Combine(dir, "PathosOfficial.dll");
            if (!File.Exists(officialDll))
            {
                error = "未能找到模组文件 PathosOfficial.dll。";
                return false;
            }

            string codexPath = Path.Combine(dir, "Assets", "Pathos.Codex");
            if (!File.Exists(codexPath))
            {
                error = "未能找到游戏数据文件 Assets/Pathos.Codex。";
                return false;
            }

            error = "";
            return true;
        }

        public static List<string> FindCandidates()
        {
            var result = new List<string>();
            var checkedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var staticPaths = new[]
            {
                @"G:\games\Pathos",
                @"C:\Games\Pathos",
                @"D:\Games\Pathos",
                @"E:\Games\Pathos",
                @"F:\Games\Pathos",
                @"C:\Program Files\Pathos",
                @"C:\Program Files (x86)\Pathos",
                @"C:\Program Files (x86)\Steam\steamapps\common\Pathos",
                @"D:\SteamLibrary\steamapps\common\Pathos",
                @"E:\SteamLibrary\steamapps\common\Pathos",
                @"F:\SteamLibrary\steamapps\common\Pathos",
                @"G:\SteamLibrary\steamapps\common\Pathos"
            };

            foreach (var p in staticPaths)
            {
                if (checkedDirs.Add(p) && Directory.Exists(p) && File.Exists(Path.Combine(p, "PathosEngine.dll")))
                {
                    result.Add(p);
                }
            }

            // Check Start Menu Shortcut
            try
            {
                string lnkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Pathos\Pathos.lnk");
                if (File.Exists(lnkPath))
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType)!;
                        dynamic shortcut = shell.CreateShortcut(lnkPath);
                        string target = shortcut.TargetPath;
                        string dir = Path.GetDirectoryName(target) ?? "";
                        if (checkedDirs.Add(dir) && Directory.Exists(dir) && File.Exists(Path.Combine(dir, "PathosEngine.dll")))
                        {
                            result.Add(dir);
                        }
                    }
                }
            }
            catch { }

            return result;
        }
    }
}
