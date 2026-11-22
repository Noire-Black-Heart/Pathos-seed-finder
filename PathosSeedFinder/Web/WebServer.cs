using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using PathosSeedFinder.Core;
using PathosSeedFinder.UI;

namespace PathosSeedFinder.Web
{
    public static class WebServer
    {
        private static HttpListener? listener;
        private static bool isRunning;
        private static string wwwrootPath = "";

        public static void Run(string[] args)
        {
            int port = 5218;

            // Resolve wwwroot path
            wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            if (!Directory.Exists(wwwrootPath))
            {
                wwwrootPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\wwwroot"));
            }

            // Attempt to initialize PathosContext from saved config
            try
            {
                var settings = SettingsManager.Load();
                if (!string.IsNullOrWhiteSpace(settings.PathosDirectory))
                {
                    PathosContext.TryInitialize(settings.PathosDirectory, out _);
                    if (PathosContext.IsInitialized)
                    {
                        WorkerPool.Prewarm(settings.PathosDirectory, WorkerPool.DefaultCores);
                    }
                }
            }
            catch { }

            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"无法在端口 {port} 启动 Web 服务器: {ex.Message}");
                Console.ResetColor();
                return;
            }

            isRunning = true;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================================");
            Console.WriteLine("  🌟 Pathos Seed Finder WebGUI 正在运行！");
            Console.WriteLine($"  🌐 访问地址: http://127.0.0.1:{port}");
            if (PathosContext.IsInitialized)
            {
                Console.WriteLine($"  🎮 已挂载游戏版本: {PathosContext.Instance!.GameVersion} ({PathosContext.Instance.InstallationPath})");
            }
            else
            {
                Console.WriteLine("  ⚠️ 尚未配置游戏目录，请在 Web 页面中设置您的 Pathos 安装位置");
            }
            Console.WriteLine("  💡 正在为您自动在浏览器中打开...");
            Console.WriteLine("  🛑 按 Ctrl + C 可关闭 Web 服务器");
            Console.WriteLine("==================================================================");
            Console.ResetColor();
            Console.WriteLine();

            // Auto open browser
            Task.Run(async () =>
            {
                await Task.Delay(600);
                try
                {
                    Process.Start(new ProcessStartInfo($"http://127.0.0.1:{port}/")
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
            });

            // Start listener loop in background task
            var listenTask = Task.Run(async () =>
            {
                while (isRunning && listener.IsListening)
                {
                    try
                    {
                        var context = await listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (HttpListenerException) when (!isRunning)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (isRunning) Console.WriteLine($"Listener error: {ex.Message}");
                    }
                }
            });

            // Keep server running indefinitely in background daemon mode
            while (isRunning)
            {
                Thread.Sleep(5000);
            }

            isRunning = false;
            listener.Stop();
            listener.Close();
        }

        private static async Task HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            // CORS headers
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 200;
                resp.Close();
                return;
            }

            string rawPath = req.Url?.AbsolutePath ?? "/";
            string path = Uri.UnescapeDataString(rawPath);

            try
            {
                if (path == "/api/settings" || path == "/api/settings/save")
                {
                    if (req.HttpMethod == "POST")
                        HandleSaveSettings(req, resp);
                    else
                        HandleGetSettings(req, resp);
                }
                else if (path == "/api/settings/browse")
                {
                    HandleBrowseFolder(req, resp);
                }
                else if (path == "/api/analyze")
                {
                    HandleAnalyze(req, resp);
                }
                else if (path == "/api/suggestions")
                {
                    HandleSuggestions(req, resp);
                }
                else if (path == "/api/filter/stream")
                {
                    await HandleFilterStream(req, resp);
                }
                else if (path == "/api/export-markdown")
                {
                    HandleExportMarkdown(req, resp);
                }
                else if (path == "/api/tile")
                {
                    HandleTile(req, resp);
                }
                else if (path == "/api/map/catalog")
                {
                    HandleMapCatalog(req, resp);
                }
                else if (path == "/api/map/data")
                {
                    HandleMapData(req, resp);
                }
                else if (path.StartsWith("/atlases/"))
                {
                    ServeAtlasesFile(path.Substring("/atlases/".Length), resp);
                }
                else
                {
                    ServeStaticFile(path, req, resp);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    resp.StatusCode = 500;
                    byte[] err = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.OutputStream.Write(err, 0, err.Length);
                    resp.Close();
                }
                catch { }
            }
        }

        private static void HandleGetSettings(HttpListenerRequest req, HttpListenerResponse resp)
        {
            bool isConfigured = PathosContext.IsInitialized;
            var settings = SettingsManager.Load();
            string currentPath = PathosContext.Instance?.InstallationPath ?? settings.PathosDirectory;
            string version = PathosContext.Instance?.GameVersion ?? "";
            string currentTileset = string.IsNullOrWhiteSpace(settings.Tileset) ? "sonyvanda" : settings.Tileset;
            string currentLang = string.IsNullOrWhiteSpace(settings.Language) ? "zh" : settings.Language;
            var candidates = SettingsManager.FindCandidates();

            string json = JsonSerializer.Serialize(new
            {
                isConfigured,
                currentPath,
                gameVersion = version,
                candidates,
                tileset = currentTileset,
                language = currentLang
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static void HandleSaveSettings(HttpListenerRequest req, HttpListenerResponse resp)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            string body = reader.ReadToEnd();
            string targetPath = "";
            string targetTileset = "sonyvanda";
            string targetLang = "";

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("path", out var p))
                {
                    targetPath = p.GetString() ?? "";
                }
                if (doc.RootElement.TryGetProperty("tileset", out var t))
                {
                    targetTileset = t.GetString() ?? "sonyvanda";
                }
                if (doc.RootElement.TryGetProperty("language", out var l))
                {
                    targetLang = l.GetString() ?? "";
                }
            }
            catch { }

            targetPath = targetPath.Trim().Trim('"');

            var currentSettings = SettingsManager.Load();
            currentSettings.Tileset = string.IsNullOrWhiteSpace(targetTileset) ? "sonyvanda" : targetTileset;
            if (!string.IsNullOrWhiteSpace(targetLang))
            {
                currentSettings.Language = targetLang;
            }
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                currentSettings.PathosDirectory = targetPath;
            }
            SettingsManager.Save(currentSettings);

            bool pathChanged = PathosContext.Instance == null ||
                               !string.Equals(PathosContext.Instance.InstallationPath, targetPath, StringComparison.OrdinalIgnoreCase);

            bool initOk = true;
            string error = "";

            if (pathChanged)
            {
                initOk = PathosContext.TryInitialize(targetPath, out error);
            }

            if (initOk)
            {
                TileService.ClearCache();
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    WorkerPool.Prewarm(targetPath, WorkerPool.DefaultCores);
                }

                string json = JsonSerializer.Serialize(new
                {
                    success = true,
                    path = PathosContext.Instance?.InstallationPath ?? targetPath,
                    version = PathosContext.Instance?.GameVersion ?? "",
                    tileset = currentSettings.Tileset,
                    language = currentSettings.Language
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                byte[] buffer = Encoding.UTF8.GetBytes(json);
                resp.ContentType = "application/json; charset=utf-8";
                resp.StatusCode = 200;
                resp.OutputStream.Write(buffer, 0, buffer.Length);
                resp.Close();
            }
            else
            {
                string json = JsonSerializer.Serialize(new
                {
                    success = false,
                    error,
                    tileset = currentSettings.Tileset,
                    language = currentSettings.Language
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                byte[] buffer = Encoding.UTF8.GetBytes(json);
                resp.ContentType = "application/json; charset=utf-8";
                resp.StatusCode = 400;
                resp.OutputStream.Write(buffer, 0, buffer.Length);
                resp.Close();
            }
        }

        private static void HandleTile(HttpListenerRequest req, HttpListenerResponse resp)
        {
            var qs = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            string name = qs["name"] ?? "";
            string? tileset = qs["tileset"];

            byte[]? pngBytes = TileService.GetTile(name, tileset);
            if (pngBytes != null && pngBytes.Length > 0)
            {
                resp.ContentType = "image/png";
                if (!string.IsNullOrWhiteSpace(tileset))
                {
                    resp.Headers.Add("Cache-Control", "public, max-age=604800");
                }
                else
                {
                    resp.Headers.Add("Cache-Control", "no-cache");
                }
                resp.StatusCode = 200;
                resp.ContentLength64 = pngBytes.Length;
                resp.OutputStream.Write(pngBytes, 0, pngBytes.Length);
                resp.Close();
            }
            else
            {
                resp.StatusCode = 404;
                resp.Close();
            }
        }

        private static void HandleBrowseFolder(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string? selected = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var dialog = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "请选择 Pathos: Nethack Codex 游戏安装根目录 (包含 PathosEngine.dll)",
                        UseDescriptionForTitle = true,
                        ShowNewFolderButton = false
                    };

                    string current = PathosContext.Instance?.InstallationPath ?? SettingsManager.Load().PathosDirectory;
                    if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                    {
                        dialog.SelectedPath = current;
                    }

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        selected = dialog.SelectedPath;
                    }
                }
                catch { }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            string json;
            if (!string.IsNullOrEmpty(selected))
            {
                bool valid = SettingsManager.ValidatePathosDirectory(selected, out string err);
                json = JsonSerializer.Serialize(new
                {
                    success = true,
                    selectedPath = selected,
                    isValid = valid,
                    error = err
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            else
            {
                json = JsonSerializer.Serialize(new
                {
                    success = false,
                    cancelled = true
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static void HandleAnalyze(HttpListenerRequest req, HttpListenerResponse resp)
        {
            var qs = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            string seed = qs["seed"] ?? "test1234";

            var report = SeedAnalyzer.Analyze(seed);
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static void HandleMapCatalog(HttpListenerRequest req, HttpListenerResponse resp)
        {
            var qs = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            string seed = qs["seed"] ?? "test1234";

            var catalog = MapService.GetCatalog(seed);
            string json = JsonSerializer.Serialize(new { maps = catalog }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static void HandleMapData(HttpListenerRequest req, HttpListenerResponse resp)
        {
            var qs = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            string seed = qs["seed"] ?? "test1234";
            string map = qs["map"] ?? "map_0";

            var mapData = MapService.GetMapData(seed, map);
            if (mapData == null)
            {
                resp.StatusCode = 404;
                byte[] notFound = Encoding.UTF8.GetBytes("{\"error\":\"Map not found\"}");
                resp.ContentType = "application/json; charset=utf-8";
                resp.OutputStream.Write(notFound, 0, notFound.Length);
                resp.Close();
                return;
            }

            string json = JsonSerializer.Serialize(mapData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static object GetArtifactSuggestions()
        {
            try
            {
                var ctx = PathosContext.Instance;
                if (ctx != null && ctx.IsReady && ctx.Manifest != null)
                {
                    dynamic manifest = ctx.Manifest;
                    dynamic itemsList = manifest.Items?.List;
                    if (itemsList != null)
                    {
                        var trans = ctx.Translations;
                        var excludedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Backpack", "Gold Key", "Ruby Key", "Shadow Key", "Silver Key", "Jade Key"
                        };

                        var list = new List<object>();
                        foreach (var item in itemsList)
                        {
                            dynamic dItem = item;
                            string gradeName = (string)(dItem.Grade?.Name ?? "");
                            if (gradeName == "artifact")
                            {
                                string en = (string)dItem.Name;
                                if (excludedKeys.Contains(en)) continue;

                                string cn = trans.TranslateItem(en);
                                string glyph = (string)(dItem.Glyph?.Name ?? dItem.Glyph?.ToString() ?? en);
                                string? alias = null;
                                if (en.Equals("Pandora's Box", StringComparison.OrdinalIgnoreCase))
                                {
                                    alias = "潘多拉之盒";
                                }
                                list.Add(new { cn, en, glyph, alias });
                            }
                        }

                        if (list.Count >= 40)
                        {
                            return list;
                        }
                    }
                }
            }
            catch { }

            // 完整 43 个核心神器 + 扩展条目 Fallback
            return new object[]
            {
                new { cn = "大师钥匙", en = "Master Key", glyph = "Master Key", alias = "" },
                new { cn = "贪婪法典", en = "Miserus", glyph = "Miserus", alias = "" },
                new { cn = "女巫扫帚", en = "Brumstyk", glyph = "Brumstyk", alias = "" },
                new { cn = "巨型挖掘者", en = "Colossal Excavator", glyph = "Colossal Excavator", alias = "" },
                new { cn = "明镜", en = "Mirrorbright", glyph = "Mirrorbright", alias = "" },
                new { cn = "逃脱专家", en = "Escapist", glyph = "Escapist", alias = "" },
                new { cn = "哲学者之石", en = "Philosophers Stone", glyph = "Philosophers Stone", alias = "" },
                new { cn = "埃塞俄比加之眼", en = "Eye of Aethiopica", glyph = "Eye of Aethiopica", alias = "" },
                new { cn = "太阳神之眼", en = "Eyes of Ra", glyph = "Eyes of Ra", alias = "" },
                new { cn = "魅血", en = "Rosenthral", glyph = "Rosenthral", alias = "" },
                new { cn = "凋零", en = "Witherloch", glyph = "Witherloch", alias = "" },
                new { cn = "光明", en = "Illuminare", glyph = "Illuminare", alias = "" },
                new { cn = "潘多拉宝盒", en = "Pandora's Box", glyph = "Pandoras Box", alias = "潘多拉之盒" },
                new { cn = "智慧之镜", en = "Prudentia", glyph = "Prudentia", alias = "" },
                new { cn = "威利姆之壁", en = "Verimurus", glyph = "Verimurus", alias = "" },
                new { cn = "泰拉瑞亚", en = "Talaria", glyph = "Talaria", alias = "" },
                new { cn = "斩龙剑", en = "Dragonbane", glyph = "Dragonbane", alias = "" },
                new { cn = "抽动之舌", en = "Lashing Tongue", glyph = "Lashing Tongue", alias = "" },
                new { cn = "撕裂者", en = "Cleaver", glyph = "Cleaver", alias = "" },
                new { cn = "裁决者", en = "Magistrator", glyph = "Magistrator", alias = "" },
                new { cn = "阳炎剑", en = "Sunsword", glyph = "Sunsword", alias = "" },
                new { cn = "佛盘剑", en = "Vorpal Blade", glyph = "Vorpal Blade", alias = "" },
                new { cn = "邪兽之牙", en = "Grimtooth", glyph = "Grimtooth", alias = "" },
                new { cn = "巨人杀手", en = "Giantslayer", glyph = "Giantslayer", alias = "" },
                new { cn = "胄甲", en = "Carapace", glyph = "Carapace", alias = "" },
                new { cn = "混乱之锤", en = "Chaoshammer", glyph = "Chaoshammer", alias = "" },
                new { cn = "仙女座", en = "Aurigage", glyph = "Aurigage", alias = "" },
                new { cn = "宵暗", en = "Blinderäg", glyph = "Blinderag", alias = "" },
                new { cn = "冰霜旋涡", en = "Maulfrost", glyph = "Maulfrost", alias = "" },
                new { cn = "雷霆长矛", en = "Voltaic", glyph = "Voltaic", alias = "" },
                new { cn = "枯枝", en = "Deadwood", glyph = "Deadwood", alias = "" },
                new { cn = "血色蔷薇", en = "Dire Needle", glyph = "Dire Needle", alias = "" },
                new { cn = "裂地锋芒", en = "Chasm Edge", glyph = "Chasm Edge", alias = "" },
                new { cn = "沙漠之鹰", en = "Deagle", glyph = "Deagle", alias = "" },
                new { cn = "三千惊雷枪", en = "Drilanze", glyph = "Drilanze", alias = "" },
                new { cn = "符文剑", en = "Runesword", glyph = "Runesword", alias = "" },
                new { cn = "古武士刀", en = "Ancient Katana", glyph = "Ancient Katana", alias = "" },
                new { cn = "正宗", en = "Masamune", glyph = "Masamune", alias = "" },
                new { cn = "村正", en = "Muramasa", glyph = "Muramasa", alias = "" },
                new { cn = "“英雄”", en = "The Hero", glyph = "The Hero", alias = "" },
                new { cn = "双子星", en = "Harbalest", glyph = "Harbalest", alias = "" },
                new { cn = "鸦羽弓", en = "Ravenbow", glyph = "Ravenbow", alias = "" },
                new { cn = "幼芽", en = "Creeping Sprout", glyph = "Creeping Sprout", alias = "" },
                new { cn = "有印章的信", en = "Stamped Letter", glyph = "Stamped Letter", alias = "" }
            };
        }

        private static void HandleSuggestions(HttpListenerRequest req, HttpListenerResponse resp)
        {
            var artifacts = GetArtifactSuggestions();

            var shopTypes = new[]
            {
                new { cn = "百货商店", en = "general store", glyph = "general store" },
                new { cn = "古兵器店", en = "antique weapons outlet", glyph = "antique weapons" },
                new { cn = "二手盔甲营销", en = "used armour dealership", glyph = "used armour" },
                new { cn = "二手文具商店", en = "second-hand stationery store", glyph = "second-hand stationery store" },
                new { cn = "灯具店", en = "lighting store", glyph = "lighting store" },
                new { cn = "优质的服装及配饰", en = "quality apparel and accessories", glyph = "quality apparel" },
                new { cn = "稀有书籍", en = "rare books", glyph = "rare books" },
                new { cn = "熟食店", en = "delicatessen", glyph = "delicatessen" },
                new { cn = "酒类商场", en = "liquor emporium", glyph = "liquor emporium" },
                new { cn = "神圣森林", en = "sacred grove", glyph = "sacred grove" },
                new { cn = "祭坛", en = "holy shrine", glyph = "holy shrine" },
                new { cn = "黑暗圣殿", en = "dark sepulchre", glyph = "dark sepulchre" },
                new { cn = "神秘据点", en = "mystic coven", glyph = "mystic coven" },
                new { cn = "五金店", en = "hardware store", glyph = "hardware store" },
                new { cn = "枪店", en = "gun mart", glyph = "gun mart" },
                new { cn = "珠宝店", en = "jewellers", glyph = "jewellers" },
                new { cn = "制作站点", en = "craft station", glyph = "craft station" }
            };

            var sokobanPrizes = new[]
            {
                new { cn = "收纳之袋", en = "bag of holding", glyph = "porter" },
                new { cn = "诡计之袋", en = "bag of tricks", glyph = "porter" },
                new { cn = "反射护符", en = "amulet of reflection", glyph = "spherical amulet" },
                new { cn = "吸收抗性护符", en = "amulet of drain resistance", glyph = "spherical amulet" },
                new { cn = "生命护符", en = "amulet of life saving", glyph = "spherical amulet" },
                new { cn = "续命护符", en = "amulet of life saving", glyph = "spherical amulet" },
                new { cn = "隐形斗篷", en = "cloak of invisibility", glyph = "tattered cape" },
                new { cn = "位移斗篷", en = "cloak of displacement", glyph = "tattered cape" },
                new { cn = "油布斗篷", en = "oilskin cloak", glyph = "tattered cape" },
                new { cn = "防护斗篷", en = "cloak of protection", glyph = "tattered cape" },
                new { cn = "隐身之戒", en = "ring of invisibility", glyph = "twisted ring" },
                new { cn = "自由行动之戒", en = "ring of free action", glyph = "twisted ring" },
                new { cn = "警告之戒", en = "ring of warning", glyph = "twisted ring" },
                new { cn = "充能魔杖", en = "wand of charging", glyph = "short wand" },
                new { cn = "严寒之杖", en = "wand of cold", glyph = "short wand" },
                new { cn = "闪电之杖", en = "wand of lightning", glyph = "short wand" },
                new { cn = "魔法飞弹之杖", en = "wand of magic missile", glyph = "short wand" }
            };

            var sites = new[]
            {
                new { cn = "主地牢", en = "Dungeon" },
                new { cn = "矿洞", en = "Mines" },
                new { cn = "地城", en = "Underdeep" },
                new { cn = "精灵王国", en = "Elf Kingdom" },
                new { cn = "卢底人堡垒", en = "Fort Ludios" },
                new { cn = "迷宫", en = "Labyrinth" },
                new { cn = "迷失之庭", en = "Lost Chambers" },
                new { cn = "美杜莎领地", en = "Medusa Lair" },
                new { cn = "巫妖之塔", en = "Lich Tower" },
                new { cn = "黑市", en = "Black Market" },
                new { cn = "深渊", en = "Abyss" }
            };

            string json = JsonSerializer.Serialize(new
            {
                artifacts,
                shopTypes,
                sokobanPrizes,
                sites,
                maxCores = WorkerPool.MaxCores,
                defaultThreads = WorkerPool.DefaultCores
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static async Task HandleFilterStream(HttpListenerRequest req, HttpListenerResponse resp)
        {
            resp.ContentType = "text/event-stream; charset=utf-8";
            resp.Headers.Add("Cache-Control", "no-cache");
            resp.Headers.Add("Connection", "keep-alive");
            resp.StatusCode = 200;

            var qs = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            var criteria = new FilterCriteria();

            if (!string.IsNullOrWhiteSpace(qs["matchMode"])) criteria.MatchMode = qs["matchMode"]!;
            if (!string.IsNullOrWhiteSpace(qs["rules"]))
            {
                string rawRules = qs["rules"]!.Trim();
                try
                {
                    var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    if (rawRules.StartsWith("["))
                    {
                        var parsedRules = JsonSerializer.Deserialize<List<ConditionRule>>(rawRules, opt);
                        if (parsedRules != null) criteria.Rules = parsedRules;
                    }
                    else
                    {
                        var singleRule = JsonSerializer.Deserialize<ConditionRule>(rawRules, opt);
                        if (singleRule != null) criteria.Rules = new List<ConditionRule> { singleRule };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to parse query rules: {ex.Message}. Raw: {rawRules}");
                }
            }
            Console.WriteLine($"[DEBUG] Rules count: {criteria.Rules?.Count}, matchMode: {criteria.MatchMode}");

            if (!string.IsNullOrWhiteSpace(qs["artifact"])) criteria.RequiredArtifact = qs["artifact"];
            if (int.TryParse(qs["level"], out int l)) criteria.RequiredLevel = l;
            if (!string.IsNullOrWhiteSpace(qs["site"])) criteria.RequiredSite = qs["site"];
            if (bool.TryParse(qs["allowCarried"], out bool ac)) criteria.RequiredArtifactAllowCarried = ac;
            if (int.TryParse(qs["minArtifacts"], out int ma)) criteria.MinArtifacts = ma;
            if (!string.IsNullOrWhiteSpace(qs["minetown"])) criteria.MinetownState = qs["minetown"];
            if (!string.IsNullOrWhiteSpace(qs["elf"])) criteria.ElfKingdomState = qs["elf"];
            if (int.TryParse(qs["minShops"], out int ms)) criteria.MinShops = ms;
            if (!string.IsNullOrWhiteSpace(qs["shopType"])) criteria.RequiredShop = qs["shopType"];
            if (int.TryParse(qs["shopMaxLevel"] ?? qs["shopLevel"], out int sml)) criteria.RequiredShopMaxLevel = sml;
            if (!string.IsNullOrWhiteSpace(qs["shopSite"])) criteria.RequiredShopSite = qs["shopSite"];
            if (!string.IsNullOrWhiteSpace(qs["sokobanPrize"])) criteria.RequiredSokobanPrize = qs["sokobanPrize"];
            if (int.TryParse(qs["sokobanLevel"], out int skl)) criteria.RequiredSokobanLevel = skl;
            if (!string.IsNullOrWhiteSpace(qs["branch"])) criteria.RequiredBranch = qs["branch"];
            if (int.TryParse(qs["branchLevel"], out int bl)) criteria.RequiredBranchLevel = bl;
            if (int.TryParse(qs["max"], out int mx)) criteria.MaxSearchCount = Math.Clamp(mx, 1, 50000);
            if (int.TryParse(qs["start"], out int strt)) criteria.StartSeedNumber = strt;
            if (!string.IsNullOrWhiteSpace(qs["prefix"])) criteria.SeedPrefix = qs["prefix"]!;

            int threads = WorkerPool.DefaultCores;
            if (int.TryParse(qs["threads"] ?? qs["workers"], out int th))
            {
                threads = Math.Clamp(th, 1, WorkerPool.MaxCores);
            }

            int tested = 0;
            int matched = 0;
            var sw = Stopwatch.StartNew();
            long lastProgressTime = 0;
            object writerLock = new();
            using var writer = new StreamWriter(resp.OutputStream, new UTF8Encoding(false));

            var jsonOpt = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            try
            {
                using var cts = new CancellationTokenSource();

                await foreach (var report in SeedFilter.SearchParallelAsync(criteria, threads, (cur, s, match, speed) =>
                {
                    tested = cur;
                    long now = sw.ElapsedMilliseconds;

                    // Throttle progress updates to at most once per 80ms or on completion
                    if (cur == criteria.MaxSearchCount || now - lastProgressTime >= 80)
                    {
                        lastProgressTime = now;
                        var progressObj = new
                        {
                            type = "progress",
                            tested = cur,
                            currentSeed = s,
                            matched = matched,
                            speed = Math.Round(speed, 1),
                            elapsedMs = now,
                            threads = threads
                        };
                        lock (writerLock)
                        {
                            try
                            {
                                writer.Write($"data: {JsonSerializer.Serialize(progressObj, jsonOpt)}\n\n");
                                writer.Flush();
                            }
                            catch { }
                        }
                    }
                }, cts.Token))
                {
                    matched++;
                    var matchObj = new
                    {
                        type = "match",
                        report = report
                    };
                    lock (writerLock)
                    {
                        try
                        {
                            writer.Write($"data: {JsonSerializer.Serialize(matchObj, jsonOpt)}\n\n");
                            writer.Flush();
                        }
                        catch { }
                    }
                }

                var doneObj = new
                {
                    type = "done",
                    totalTested = tested,
                    totalMatches = matched,
                    elapsedMs = sw.ElapsedMilliseconds,
                    speed = sw.ElapsedMilliseconds > 0 ? Math.Round(tested / (sw.ElapsedMilliseconds / 1000.0), 1) : 0,
                    threads = threads
                };
                lock (writerLock)
                {
                    try
                    {
                        writer.Write($"data: {JsonSerializer.Serialize(doneObj, jsonOpt)}\n\n");
                        writer.Flush();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STREAM] Stream ended/cancelled: {ex.Message}");
            }
            finally
            {
                try { resp.Close(); } catch { }
            }
        }

        private static void HandleExportMarkdown(HttpListenerRequest req, HttpListenerResponse resp)
        {
            var qs = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            string seed = qs["seed"] ?? "test1234";

            var report = SeedAnalyzer.Analyze(seed);
            string md = ConsoleFormatter.GenerateMarkdownReport(report);

            byte[] buffer = Encoding.UTF8.GetBytes(md);
            resp.ContentType = "text/markdown; charset=utf-8";
            resp.Headers.Add("Content-Disposition", $"attachment; filename=\"Pathos_Seed_{report.SeedText.TrimStart('#')}.md\"");
            resp.StatusCode = 200;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
            resp.Close();
        }

        private static void ServeAtlasesFile(string subPath, HttpListenerResponse resp)
        {
            // Check user's configured Pathos directory
            string? activeDir = PathosContext.Instance?.InstallationPath ?? SettingsManager.Load().PathosDirectory;
            if (!string.IsNullOrEmpty(activeDir))
            {
                string gameAtlases = Path.Combine(activeDir, "Atlases", subPath);
                if (File.Exists(gameAtlases))
                {
                    ServeFileDirect(gameAtlases, null, resp);
                    return;
                }

                string gameAssets = Path.Combine(activeDir, "Assets", subPath);
                if (File.Exists(gameAssets))
                {
                    ServeFileDirect(gameAssets, null, resp);
                    return;
                }
            }

            resp.StatusCode = 404;
            resp.Close();
        }

        private static void ServeStaticFile(string path, HttpListenerRequest req, HttpListenerResponse resp)
        {
            if (path == "/" || string.IsNullOrEmpty(path)) path = "/index.html";

            string localPath = Path.GetFullPath(Path.Combine(wwwrootPath, path.TrimStart('/')));
            if (!localPath.StartsWith(wwwrootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(localPath))
            {
                resp.StatusCode = 404;
                byte[] notFound = Encoding.UTF8.GetBytes("404 Not Found");
                resp.OutputStream.Write(notFound, 0, notFound.Length);
                resp.Close();
                return;
            }

            ServeFileDirect(localPath, req, resp);
        }

        private static void ServeFileDirect(string filePath, HttpListenerRequest? req, HttpListenerResponse resp)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            resp.ContentType = ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".json" => "application/json; charset=utf-8",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };

            using var fs = File.OpenRead(filePath);
            resp.StatusCode = 200;
            resp.ContentLength64 = fs.Length;

            // Only write body for non-HEAD requests
            if (req == null || req.HttpMethod != "HEAD")
            {
                fs.CopyTo(resp.OutputStream);
            }
            resp.Close();
        }
    }
}
