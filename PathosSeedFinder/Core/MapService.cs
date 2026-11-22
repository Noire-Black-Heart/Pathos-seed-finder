using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PathosSeedFinder.Core
{
    public static class MapService
    {
        private static readonly object simulationLock = new();
        private static string cachedSeedInput = "";
        private static int cachedSeedCode = 0;
        private static dynamic? cachedAdventure = null;
        private static List<MapCatalogItem>? cachedCatalog = null;

        public static void CacheAdventure(string seedInput, int seedCode, dynamic adventure)
        {
            lock (simulationLock)
            {
                cachedSeedInput = seedInput;
                cachedSeedCode = seedCode;
                cachedAdventure = adventure;
                cachedCatalog = null; // Invalidate catalog so it builds on demand
            }
        }

        private static dynamic EnsureSimulation(string seedInput, out int seedCode)
        {
            var ctx = PathosContext.Instance;
            if (ctx == null || !ctx.IsReady)
            {
                throw new InvalidOperationException("尚未配置有效的 Pathos 游戏安装目录！");
            }

            seedInput = seedInput.Trim();
            dynamic seed;
            if (seedInput.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                seed = ctx.CreateSeed(seedInput);
            }
            else if (Regex.IsMatch(seedInput, @"\A\b[0-9a-fA-F]{1,8}\b\Z"))
            {
                seed = ctx.CreateSeed("#" + seedInput);
            }
            else if (int.TryParse(seedInput, out int numericCode))
            {
                seed = ctx.CreateSeedFromCode(numericCode);
            }
            else
            {
                seed = ctx.CreateSeed(seedInput);
            }

            seedCode = (int)seed.Code;

            lock (simulationLock)
            {
                if (cachedAdventure != null && (cachedSeedInput.Equals(seedInput, StringComparison.OrdinalIgnoreCase) || cachedSeedCode == seedCode))
                {
                    return cachedAdventure!;
                }

                dynamic engine = ctx.CreateEngine(seed);
                dynamic adventure = engine.Adventure;
                adventure.ChapterList.Clear();
                adventure.PlayerList.Clear();
                ctx.Reseed(Environment.TickCount);
                ctx.Reseed(seedCode);
                ctx.PrepareShuffle(adventure);
                ctx.NethackModule.Execute(engine.Generator);

                cachedSeedInput = seedInput;
                cachedSeedCode = seedCode;
                cachedAdventure = adventure;
                cachedCatalog = null;

                return adventure;
            }
        }

        public static List<MapCatalogItem> GetCatalog(string seedInput)
        {
            var adventure = EnsureSimulation(seedInput, out _);
            var trans = PathosContext.Instance!.Translations;

            lock (simulationLock)
            {
                if (cachedCatalog != null)
                {
                    return cachedCatalog;
                }

                var catalog = new List<MapCatalogItem>();
                int index = 0;

                foreach (var m in (IEnumerable)adventure.World.Maps)
                {
                    dynamic dm = m;
                    string rawName = (string)dm.Name;
                    var (group, displayName, site, levelIndex) = CategorizeMap(rawName, trans);

                    int w = (int)dm.Region.Width;
                    int h = (int)dm.Region.Height;

                    catalog.Add(new MapCatalogItem
                    {
                        Id = $"map_{index}",
                        RawName = rawName,
                        DisplayName = displayName,
                        Group = group,
                        Site = site,
                        SiteChinese = trans.TranslateSite(site),
                        LevelIndex = levelIndex,
                        Width = w,
                        Height = h
                    });

                    index++;
                }

                // Sort catalog: by group priority, then level index, then display name
                cachedCatalog = catalog
                    .OrderBy(c => GetGroupPriority(c.Group))
                    .ThenBy(c => c.LevelIndex)
                    .ThenBy(c => c.DisplayName)
                    .ToList();

                return cachedCatalog;
            }
        }

        private static int GetGroupPriority(string g) => g switch
        {
            "🏰 主地牢 (1~40层)" => 1,
            "⛏️ 矮人矿洞 (1~5层)" => 2,
            "⛏️ 幽暗地城 (Underdeep)" => 3,
            "📦 仓库番解谜 (1~4层)" => 4,
            "🌀 迷宫挑战 (Labyrinth)" => 5,
            "💀 巫妖之塔 (Lich Tower)" => 6,
            "🐍 美杜莎领地 (Medusa Lair)" => 7,
            "🧝 精灵王国 (Elf Kingdom)" => 8,
            "💰 卢底人要塞 (Fort Ludios)" => 9,
            "🏛️ 特殊支线与黑市" => 10,
            "🔥 地狱炼狱 (Infernal Realms)" => 11,
            "🔍 地牢隐秘附层" => 12,
            _ => 99
        };

        private static (string group, string displayName, string site, int levelIndex) CategorizeMap(string rawName, TranslationManager trans)
        {
            string clean = rawName.Trim().Trim('_').Trim();

            // 1. Main Dungeon and Dungeon Sub-levels (Attic, Cellar, Trove, Caves)
            var dungeonMatch = Regex.Match(rawName, @"^(?:_?level//depth|_?Dungeon)\D*(\d+)(.*)", RegexOptions.IgnoreCase);
            if (dungeonMatch.Success)
            {
                int lvl = int.Parse(dungeonMatch.Groups[1].Value);
                string suffix = dungeonMatch.Groups[2].Value.Trim().Trim('_').Trim();

                if (string.IsNullOrEmpty(suffix))
                {
                    return ("🏰 主地牢 (1~40层)", $"主地牢 第 {lvl} 层", "Dungeon", lvl);
                }
                else
                {
                    string subCn = suffix.ToLowerInvariant() switch
                    {
                        "attic" => "阁楼",
                        "cellar" => "地窖",
                        "trove" => "藏宝密室",
                        "caves" => "洞穴",
                        _ => trans.TranslateSite(suffix)
                    };
                    return ("🔍 地牢隐秘附层", $"主地牢 第 {lvl} 层 ({subCn})", $"Dungeon ({suffix})", lvl);
                }
            }

            // 2. Mines
            if (rawName.Equals("_Minetown_", StringComparison.OrdinalIgnoreCase) || rawName.Equals("Minetown", StringComparison.OrdinalIgnoreCase))
            {
                return ("⛏️ 矮人矿洞 (1~5层)", "矿洞 第 1 层 (矿镇)", "Mines", 1);
            }
            if (rawName.StartsWith("_Mines_", StringComparison.OrdinalIgnoreCase) || rawName.StartsWith("Mines", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(rawName, @"\d+");
                int lvl = match.Success ? int.Parse(match.Value) : 1;
                return ("⛏️ 矮人矿洞 (1~5层)", $"矿洞 第 {lvl} 层", "Mines", lvl);
            }

            // 3. Underdeep
            if (clean.StartsWith("Under", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Deep", StringComparison.OrdinalIgnoreCase))
            {
                string siteCn = trans.TranslateSite(clean);
                int lvl = clean switch
                {
                    "Underpeaks" => 1,
                    "Underlimits" => 2,
                    "Undertown" => 3,
                    "Underbarracks" => 4,
                    "Deepwilds" => 5,
                    "Deepruins" => 6,
                    "Deephall" => 7,
                    _ => 8
                };
                string name = clean switch
                {
                    "Underpeaks" => "地城通路 (Underpeaks)",
                    "Underlimits" => "地城边境 (Underlimits)",
                    "Undertown" => "地下城 (Undertown)",
                    "Underbarracks" => "地城兵营 (Underbarracks)",
                    "Deepwilds" => "地下荒野 (Deepwilds)",
                    "Deepruins" => "地下废墟 (Deepruins)",
                    "Deephall" => "地下大厅 (Deephall)",
                    _ => !string.IsNullOrEmpty(siteCn) ? $"{siteCn} ({clean})" : clean
                };
                return ("⛏️ 幽暗地城 (Underdeep)", name, "Underdeep", lvl);
            }

            // 4. Sokoban
            if (clean.StartsWith("Sokoban", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(clean, @"\d+");
                int lvl = match.Success ? int.Parse(match.Value) : 1;
                return ("📦 仓库番解谜 (1~4层)", $"仓库番 第 {lvl} 层", "Sokoban", lvl);
            }

            // 5. Labyrinth
            if (clean.StartsWith("Labyrinth", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(clean, @"\d+");
                int lvl = match.Success ? int.Parse(match.Value) : 1;
                return ("🌀 迷宫挑战 (Labyrinth)", $"迷宫 第 {lvl} 层", "Labyrinth", lvl);
            }

            // 6. Lich Tower
            if (clean.StartsWith("Lich", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(clean, @"\d+");
                int lvl = match.Success ? int.Parse(match.Value) : 1;
                return ("💀 巫妖之塔 (Lich Tower)", $"巫妖之塔 第 {lvl} 层", "Lich Tower", lvl);
            }

            // 7. Medusa's Lair
            if (clean.StartsWith("Medusa", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(clean, @"\d+");
                int lvl = match.Success ? int.Parse(match.Value) : 1;
                return ("🐍 美杜莎领地 (Medusa Lair)", $"美杜莎领地 第 {lvl} 层", "Medusa Lair", lvl);
            }

            // 8. Elf Kingdom
            if (clean.StartsWith("Elf", StringComparison.OrdinalIgnoreCase))
            {
                int lvl = clean switch
                {
                    "Elf Forest" => 1,
                    "Elf Town" => 2,
                    "Elf Town Trove" => 3,
                    "Elf Palace 1" => 4,
                    "Elf Palace 2" => 5,
                    "Elf Palace Vault" => 6,
                    _ => 7
                };
                string name = clean switch
                {
                    "Elf Forest" => "精灵森林 (Elf Forest)",
                    "Elf Town" => "精灵城镇 (Elf Town)",
                    "Elf Town Trove" => "精灵城地窖 (Trove)",
                    "Elf Palace 1" => "精灵宫殿 第 1 层",
                    "Elf Palace 2" => "精灵宫殿 第 2 层",
                    "Elf Palace Vault" => "精灵宫殿顶层 (Vault)",
                    _ => trans.TranslateSite(clean)
                };
                return ("🧝 精灵王国 (Elf Kingdom)", name, "Elf Kingdom", lvl);
            }

            // 9. Fort Ludios
            if (clean.StartsWith("Fort Ludios", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Ludios", StringComparison.OrdinalIgnoreCase))
            {
                int lvl = clean.Contains("Fort") ? 1 : 2;
                string name = clean.Contains("Fort") ? "卢底人堡垒 (Fort Ludios)" : "卢底人藏宝室 (Cache)";
                return ("💰 卢底人要塞 (Fort Ludios)", name, "Fort Ludios", lvl);
            }

            // 10. Infernal Realms
            if (clean.StartsWith("Infernal", StringComparison.OrdinalIgnoreCase))
            {
                int lvl = clean switch
                {
                    "Infernal Fields" => 1,
                    "Infernal Keep" => 2,
                    "Infernal Spire" => 3,
                    "Infernal Cage" => 4,
                    _ => 5
                };
                string name = clean switch
                {
                    "Infernal Fields" => "地狱场地 (Infernal Fields)",
                    "Infernal Keep" => "地狱看守 (Infernal Keep)",
                    "Infernal Spire" => "地狱尖塔 (Infernal Spire)",
                    "Infernal Cage" => "地狱牢笼 (Infernal Cage)",
                    _ => trans.TranslateSite(clean)
                };
                return ("🔥 地狱炼狱 (Infernal Realms)", name, "Infernal", lvl);
            }

            // 11. Special branches & Black Market
            if (clean.Equals("Black Market", StringComparison.OrdinalIgnoreCase))
            {
                return ("🏛️ 特殊支线与黑市", "黑市 (Black Market)", "Black Market", 1);
            }
            if (clean.Equals("Lost Chambers", StringComparison.OrdinalIgnoreCase))
            {
                return ("🏛️ 特殊支线与黑市", "迷失之庭 (Lost Chambers)", "Lost Chambers", 2);
            }
            if (clean.Equals("Abyss", StringComparison.OrdinalIgnoreCase))
            {
                return ("🏛️ 特殊支线与黑市", "深渊 (Abyss)", "Abyss", 3);
            }

            // 12. Fallback
            string fallbackCn = trans.TranslateSite(clean);
            return ("其它特殊区域", !string.IsNullOrEmpty(fallbackCn) ? fallbackCn : clean, clean, 99);
        }

        public static FloorMapResponse? GetMapData(string seedInput, string mapIdOrName)
        {
            var adventure = EnsureSimulation(seedInput, out int seedCode);
            var trans = PathosContext.Instance!.Translations;
            var catalog = GetCatalog(seedInput);

            MapCatalogItem? meta = null;
            if (mapIdOrName.StartsWith("map_", StringComparison.OrdinalIgnoreCase))
            {
                meta = catalog.FirstOrDefault(c => c.Id.Equals(mapIdOrName, StringComparison.OrdinalIgnoreCase));
            }
            if (meta == null)
            {
                meta = catalog.FirstOrDefault(c => c.RawName.Equals(mapIdOrName, StringComparison.OrdinalIgnoreCase) ||
                                                   c.DisplayName.Equals(mapIdOrName, StringComparison.OrdinalIgnoreCase))
                       ?? catalog.FirstOrDefault();
            }

            if (meta == null) return null;

            dynamic? targetMap = null;
            foreach (var m in (IEnumerable)adventure.World.Maps)
            {
                dynamic dm = m;
                if ((string)dm.Name == meta.RawName)
                {
                    targetMap = dm;
                    break;
                }
            }

            if (targetMap == null) return null;

            int left = (int)targetMap.Region.Left;
            int top = (int)targetMap.Region.Top;
            int w = (int)targetMap.Region.Width;
            int h = (int)targetMap.Region.Height;

            var tiles = new List<MapTileData>();
            int bouldersCount = 0;
            int trapsCount = 0;

            foreach (var sq in (IEnumerable)targetMap.GetSquares())
            {
                dynamic dsq = sq;

                // Skip completely empty void
                if (dsq.Floor == null && dsq.Wall == null && dsq.Door == null && dsq.Passage == null && dsq.Fixture == null && dsq.Spill == null && dsq.Boulder == null && dsq.Trap == null)
                {
                    continue;
                }

                int x = (int)dsq.X - left;
                int y = (int)dsq.Y - top;

                string? floor = dsq.Floor?.Ground?.ToString();
                string? floorGlyph = floor;

                string? wall = dsq.Wall?.Barrier?.ToString();
                string? wallSegment = dsq.Wall?.Segment?.ToString();
                string? wallGlyph = dsq.Wall?.Glyph?.ToString();

                string? door = dsq.Door?.Gate?.ToString();
                string? doorState = dsq.Door?.State?.ToString();
                string? doorGlyph = dsq.Door?.Glyph?.ToString();
                bool isSecretDoor = (bool)(dsq.Door?.Secret ?? false);

                string? passage = dsq.Passage?.Portal?.ToString();
                string? passageGlyph = passage;
                string? passageDest = dsq.Passage?.Destination?.ToString();

                string? fixture = dsq.Fixture?.Feature?.ToString();
                string? fixtureGlyph = dsq.Fixture?.Glyph?.ToString();

                string? spill = dsq.Spill?.ToString();
                string? spillGlyph = spill;

                string? boulder = dsq.Boulder?.Block?.ToString();
                string? boulderGlyph = dsq.Boulder?.Block?.Glyph?.ToString() ?? boulder;
                if (!string.IsNullOrEmpty(boulder)) bouldersCount++;

                string? trap = dsq.Trap?.Device?.ToString();
                string? trapGlyph = dsq.Trap?.Device?.Glyph?.ToString() ?? trap;
                bool? trapRevealed = dsq.Trap != null ? (bool?)dsq.Trap.Revealed : null;
                if (!string.IsNullOrEmpty(trap)) trapsCount++;

                // Determine primary top-level glyph
                string? primaryGlyph = null;
                if (!string.IsNullOrEmpty(boulderGlyph)) primaryGlyph = boulderGlyph;
                else if (!string.IsNullOrEmpty(passageGlyph)) primaryGlyph = passageGlyph;
                else if (!string.IsNullOrEmpty(doorGlyph)) primaryGlyph = doorGlyph;
                else if (!string.IsNullOrEmpty(fixtureGlyph)) primaryGlyph = fixtureGlyph;
                else if (!string.IsNullOrEmpty(trapGlyph)) primaryGlyph = trapGlyph;
                else if (!string.IsNullOrEmpty(spillGlyph)) primaryGlyph = spillGlyph;
                else if (!string.IsNullOrEmpty(wallGlyph)) primaryGlyph = wallGlyph;
                else primaryGlyph = floorGlyph;

                // Build friendly tooltip
                string tooltip = BuildTileTooltip(x + left, y + top, dsq, trans);

                tiles.Add(new MapTileData
                {
                    X = x,
                    Y = y,
                    Floor = floor,
                    FloorGlyph = floorGlyph,
                    Wall = wall,
                    WallSegment = wallSegment,
                    WallGlyph = wallGlyph,
                    Door = door,
                    DoorState = doorState,
                    DoorGlyph = doorGlyph,
                    IsSecretDoor = isSecretDoor,
                    Passage = passage,
                    PassageGlyph = passageGlyph,
                    PassageDest = passageDest,
                    Fixture = fixture,
                    FixtureGlyph = fixtureGlyph,
                    Spill = spill,
                    SpillGlyph = spillGlyph,
                    Boulder = boulder,
                    BoulderGlyph = boulderGlyph,
                    Trap = trap,
                    TrapGlyph = trapGlyph,
                    TrapRevealed = trapRevealed,
                    PrimaryGlyph = primaryGlyph,
                    Tooltip = tooltip
                });
            }

            // Extract shops, merchants and special facilities
            var shops = ExtractShops(targetMap, meta.RawName, left, top, trans);
            var items = ExtractItems(targetMap, left, top, trans);

            return new FloorMapResponse
            {
                SeedText = seedInput,
                SeedCode = seedCode,
                MapId = meta.Id,
                MapName = meta.RawName,
                DisplayName = meta.DisplayName,
                Group = meta.Group,
                Width = w,
                Height = h,
                Left = left,
                Top = top,
                BouldersCount = bouldersCount,
                TrapsCount = trapsCount,
                ItemsCount = items.Count,
                Shops = shops,
                Items = items,
                Tiles = tiles
            };
        }

        private static List<MapItemData> ExtractItems(dynamic targetMap, int left, int top, TranslationManager trans)
        {
            var items = new List<MapItemData>();

            try
            {
                // 1. Squares: Ground items, covered items under boulders, and items in containers
                foreach (var sq in (IEnumerable)targetMap.GetSquares())
                {
                    dynamic dsq = sq;
                    int x = (int)dsq.X - left;
                    int y = (int)dsq.Y - top;

                    bool hasBoulder = dsq.Boulder != null;
                    string boulderName = hasBoulder ? (string)(dsq.Boulder.Block?.ToString() ?? "") : "";
                    string boulderCn = !string.IsNullOrEmpty(boulderName) ? trans.TranslateBlock(boulderName) : "";

                    foreach (var a in (IEnumerable)dsq.GetAssets())
                    {
                        dynamic da = a;
                        dynamic item = da.Item;

                        if (item != null)
                        {
                            string iname = (string)(item.Name ?? "");
                            string inameCn = trans.TranslateItem(iname);
                            string glyph = (string)(item.Glyph?.Name ?? item.Glyph?.ToString() ?? item.Appearance?.Name ?? iname);
                            bool isArt = item.Grade != null && (string)(item.Grade.Name ?? "") == "artifact";
                            int count = (int)da.Quantity;

                            items.Add(new MapItemData
                            {
                                X = x,
                                Y = y,
                                Name = iname,
                                NameChinese = inameCn,
                                Glyph = glyph,
                                State = hasBoulder ? "covered" : "ground",
                                IsArtifact = isArt,
                                BoulderName = boulderName,
                                BoulderChinese = boulderCn,
                                Count = count
                            });
                        }

                        if (da.Container != null && da.Container.Stash != null)
                        {
                            string containerName = (string)(da.Item?.Name ?? "container");
                            string containerCn = trans.TranslateItem(containerName);

                            IEnumerable? innerAssets = null;
                            try { innerAssets = (IEnumerable?)da.Container.Stash.Assets; } catch { }
                            if (innerAssets == null)
                            {
                                try { innerAssets = (IEnumerable?)da.Container.Stash.GetAssets(); } catch { }
                            }

                            if (innerAssets != null)
                            {
                                foreach (var inside in innerAssets)
                                {
                                    dynamic dinside = inside;
                                    dynamic inItem = dinside.Item;
                                    if (inItem != null)
                                    {
                                        string iname = (string)(inItem.Name ?? "");
                                        string inameCn = trans.TranslateItem(iname);
                                        string glyph = (string)(inItem.Glyph?.Name ?? inItem.Glyph?.ToString() ?? inItem.Appearance?.Name ?? iname);
                                        bool isArt = inItem.Grade != null && (string)(inItem.Grade.Name ?? "") == "artifact";
                                        int count = (int)dinside.Quantity;

                                        items.Add(new MapItemData
                                        {
                                            X = x,
                                            Y = y,
                                        Name = iname,
                                        NameChinese = inameCn,
                                        Glyph = glyph,
                                        State = hasBoulder ? "covered" : "container",
                                        IsArtifact = isArt,
                                        ContainerName = containerName,
                                        ContainerChinese = containerCn,
                                        BoulderName = boulderName,
                                        BoulderChinese = boulderCn,
                                        Count = count
                                    });
                                }
                            }
                        }
                    }
                    }
                }

                // 2. Characters / Monsters: Equipped and carried items
                foreach (var ch in (IEnumerable)targetMap.GetCharacters())
                {
                    dynamic dc = ch;
                    if (dc.Square == null || dc.Inventory == null) continue;

                    int cx = (int)dc.Square.X - left;
                    int cy = (int)dc.Square.Y - top;
                    string cname = (string)(dc.Name ?? dc.Entity?.Name ?? "character");
                    string cnameCn = trans.TranslateEntity(cname);

                    foreach (var a in (IEnumerable)dc.Inventory.GetPossessedAssets())
                    {
                        dynamic da = a;
                        dynamic item = da.Item;
                        if (item != null)
                        {
                            string iname = (string)(item.Name ?? "");
                            string inameCn = trans.TranslateItem(iname);
                            string glyph = (string)(item.Glyph?.Name ?? item.Glyph?.ToString() ?? item.Appearance?.Name ?? iname);
                            bool isArt = item.Grade != null && (string)(item.Grade.Name ?? "") == "artifact";
                            int count = (int)da.Quantity;

                            items.Add(new MapItemData
                            {
                                X = cx,
                                Y = cy,
                                Name = iname,
                                NameChinese = inameCn,
                                Glyph = glyph,
                                State = "carried",
                                IsArtifact = isArt,
                                CarrierName = cname,
                                CarrierChinese = cnameCn,
                                Count = count
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExtractItems ERROR on {targetMap?.Name}: {ex.Message} \n{ex.StackTrace}");
            }

            return items;
        }

        private static List<ShopMarkerData> ExtractShops(dynamic targetMap, string mapRawName, int left, int top, TranslationManager trans)
        {
            var shops = new List<ShopMarkerData>();
            var occupied = new HashSet<(int x, int y)>();

            // 1. Zones with triggers (official shops and special shrines/stations)
            if (targetMap.Zones != null)
            {
                foreach (var z in (IEnumerable)targetMap.Zones)
                {
                    dynamic dz = z;
                    if (dz.Trigger != null && dz.Trigger.Schedules != null)
                    {
                        foreach (var sc in (IEnumerable)dz.Trigger.Schedules)
                        {
                            dynamic dsc = sc;
                            dynamic? routine = dsc.Routine;
                            if (routine == null) continue;

                            string trickName = routine.Trick?.Name?.ToString() ?? "";
                            if (!trickName.StartsWith("visited ", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            string rawType = trickName.Substring("visited ".Length).Trim();
                            dynamic? targetSq = routine.Target;
                            if (targetSq == null) continue;

                            int gx = (int)targetSq.X;
                            int gy = (int)targetSq.Y;
                            int lx = gx - left;
                            int ly = gy - top;

                            string? mName = null;
                            string? mGender = null;
                            if (targetSq.Character != null)
                            {
                                mName = targetSq.Character.Name?.ToString() ?? targetSq.Character.Entity?.Name?.ToString();
                                mGender = targetSq.Character.Gender?.ToString();
                            }

                            // If not standing on targetSq directly, search inside zone boundary
                            if (string.IsNullOrEmpty(mName))
                            {
                                int bLeft = (int)dz.BoundaryLeft;
                                int bTop = (int)dz.BoundaryTop;
                                int bRight = (int)dz.BoundaryRight;
                                int bBottom = (int)dz.BoundaryBottom;

                                foreach (var c in (IEnumerable)targetMap.GetCharacters())
                                {
                                    dynamic dc = c;
                                    string ename = dc.Entity?.Name?.ToString() ?? "";
                                    if (ename.Contains("merchant", StringComparison.OrdinalIgnoreCase) || ename.Contains("keeper", StringComparison.OrdinalIgnoreCase))
                                    {
                                        int cx = (int)dc.Square.X;
                                        int cy = (int)dc.Square.Y;
                                        if (cx >= bLeft && cx <= bRight && cy >= bTop && cy <= bBottom)
                                        {
                                            mName = dc.Name?.ToString() ?? ename;
                                            mGender = dc.Gender?.ToString();
                                            break;
                                        }
                                    }
                                }
                            }

                            string glyph = ResolveShopGlyph(rawType);
                            string cn = trans.TranslateShop(rawType);
                            bool isShrineOrStation = IsShrineOrStationType(rawType);
                            string tooltip = BuildShopTooltip(cn, rawType, mName, mGender, isShrineOrStation, gx, gy);

                            shops.Add(new ShopMarkerData
                            {
                                X = lx,
                                Y = ly,
                                GlobalX = gx,
                                GlobalY = gy,
                                ShopType = rawType,
                                DisplayName = cn,
                                MerchantName = mName,
                                MerchantGender = mGender,
                                Glyph = glyph,
                                Tooltip = tooltip,
                                IsShrineOrStation = isShrineOrStation,
                                ZoneLeft = (int)dz.BoundaryLeft - left,
                                ZoneTop = (int)dz.BoundaryTop - top,
                                ZoneRight = (int)dz.BoundaryRight - left,
                                ZoneBottom = (int)dz.BoundaryBottom - top
                            });

                            occupied.Add((gx, gy));
                        }
                    }
                }
            }

            // 2. Extra merchants not associated with zoned triggers (e.g. Black Market stalls)
            foreach (var c in (IEnumerable)targetMap.GetCharacters())
            {
                dynamic dc = c;
                string ename = dc.Entity?.Name?.ToString() ?? "";
                if (ename.Contains("merchant", StringComparison.OrdinalIgnoreCase) || ename.Contains("keeper", StringComparison.OrdinalIgnoreCase))
                {
                    int gx = (int)dc.Square.X;
                    int gy = (int)dc.Square.Y;
                    if (occupied.Contains((gx, gy))) continue;

                    int lx = gx - left;
                    int ly = gy - top;

                    string mName = dc.Name?.ToString() ?? ename;
                    string mGender = dc.Gender?.ToString() ?? "";

                    bool isBlackMarket = mapRawName.Contains("Black Market", StringComparison.OrdinalIgnoreCase);
                    string shopType = isBlackMarket ? "black market" : "merchant";
                    string displayName = isBlackMarket ? "黑市商人" : "商人";
                    string glyph = "general stock";

                    string tooltip = BuildShopTooltip(displayName, shopType, mName, mGender, false, gx, gy);

                    shops.Add(new ShopMarkerData
                    {
                        X = lx,
                        Y = ly,
                        GlobalX = gx,
                        GlobalY = gy,
                        ShopType = shopType,
                        DisplayName = displayName,
                        MerchantName = mName,
                        MerchantGender = mGender,
                        Glyph = glyph,
                        Tooltip = tooltip,
                        IsShrineOrStation = false
                    });

                    occupied.Add((gx, gy));
                }
            }

            return shops;
        }

        private static string ResolveShopGlyph(string shopType)
        {
            string lower = shopType.ToLowerInvariant();
            if (lower.Contains("antique") || lower.Contains("weapon")) return "weapon stock";
            if (lower.Contains("stationery") || lower.Contains("scroll")) return "scroll stock";
            if (lower.Contains("liquor") || lower.Contains("potion")) return "potion stock";
            if (lower.Contains("used armour") || lower.Contains("used armor")) return "armour stock";
            if (lower.Contains("delicatessen") || lower.Contains("food") || lower.Contains("grocery")) return "food stock";
            if (lower.Contains("jewel") || lower.Contains("gem") || lower.Contains("ring")) return "ring stock";
            if (lower.Contains("quality apparel") || lower.Contains("accessories") || lower.Contains("wand")) return "wand stock";
            if (lower.Contains("hardware") || lower.Contains("tool")) return "tool stock";
            if (lower.Contains("book")) return "book stock";
            if (lower.Contains("light")) return "light stock";
            if (lower.Contains("gun") || lower.Contains("firearm")) return "firearm stock";
            if (lower.Contains("craft station") || lower.Contains("workbench")) return "craft station";
            if (lower.Contains("holy shrine") || lower.Contains("shrine")) return "holy shrine";
            if (lower.Contains("dark sepulchre") || lower.Contains("sepulchre")) return "dark sepulchre";
            if (lower.Contains("sacred grove") || lower.Contains("grove")) return "sacred grove";
            if (lower.Contains("mystic coven") || lower.Contains("coven")) return "mystic coven";
            if (lower.Contains("general")) return "general stock";

            return "general stock";
        }

        private static bool IsShrineOrStationType(string shopType)
        {
            string lower = shopType.ToLowerInvariant();
            return lower.Contains("shrine") || lower.Contains("station") || lower.Contains("sepulchre") || lower.Contains("grove") || lower.Contains("coven");
        }

        private static string BuildShopTooltip(string cnName, string enName, string? merchantName, string? gender, bool isFacility, int gx, int gy)
        {
            string icon = isFacility ? "✨" : "🏪";
            string genderStr = gender == "female" ? " (女)" : gender == "male" ? " (男)" : "";
            string keeperStr = !string.IsNullOrWhiteSpace(merchantName) ? $" · 店主: {merchantName}{genderStr}" : "";
            return $"{icon} {cnName} ({enName}){keeperStr} | 坐标: ({gx}, {gy})";
        }

        private static string BuildTileTooltip(int globalX, int globalY, dynamic dsq, TranslationManager trans)
        {
            var parts = new List<string>();

            if (dsq.Passage != null)
            {
                string pName = dsq.Passage.Portal?.ToString() ?? "传送门";
                string cn = trans.TranslatePortal(pName);
                string dest = dsq.Passage.Destination?.ToString() ?? "";
                parts.Add(!string.IsNullOrEmpty(dest) ? $"🚪 {cn} ➜ {dest}" : $"🚪 {cn}");
            }

            if (dsq.Door != null)
            {
                string dName = dsq.Door.Gate?.ToString() ?? "门";
                string cn = trans.TranslateBlock(dName);
                string state = dsq.Door.State?.ToString() switch
                {
                    "Open" => "开启",
                    "Closed" => "关闭",
                    "Locked" => "上锁",
                    "Broken" => "损坏",
                    _ => dsq.Door.State?.ToString() ?? ""
                };
                string secret = dsq.Door.Secret == true ? " [隐秘暗门]" : "";
                parts.Add($"🚪 {cn} [{state}]{secret}");
            }

            if (dsq.Fixture != null)
            {
                string fName = dsq.Fixture.Feature?.ToString() ?? "";
                string cn = trans.TranslateBlock(fName);
                parts.Add($"✨ {cn}");
            }

            if (dsq.Boulder != null)
            {
                string bName = dsq.Boulder.Block?.ToString() ?? "巨石";
                string cn = trans.TranslateBlock(bName);
                parts.Add($"🪨 {cn}");
            }

            if (dsq.Trap != null)
            {
                string trName = dsq.Trap.Device?.ToString() ?? "陷阱";
                string cn = trans.TranslateTrap(trName);
                string rev = dsq.Trap.Revealed == true ? " [显形]" : " [隐蔽]";
                parts.Add($"⚠️ {cn}{rev}");
            }

            if (dsq.Spill != null)
            {
                string sName = dsq.Spill?.ToString() ?? "";
                string cn = trans.TranslateBlock(sName);
                parts.Add($"💧 {cn}");
            }

            if (dsq.Wall != null)
            {
                string wName = dsq.Wall.Barrier?.ToString() ?? "墙壁";
                string cn = trans.TranslateBlock(wName);
                parts.Add($"🧱 {cn}");
            }

            if (dsq.Floor != null)
            {
                string flName = dsq.Floor.Ground?.ToString() ?? "地面";
                string cn = trans.TranslateBlock(flName);
                parts.Add($"▫️ {cn}");
            }

            return $"坐标: ({globalX}, {globalY}) | " + string.Join(" · ", parts);
        }
    }
}
