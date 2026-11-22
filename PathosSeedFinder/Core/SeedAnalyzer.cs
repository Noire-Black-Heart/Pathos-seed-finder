using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PathosSeedFinder.Core
{
    public class SeedAnalyzer
    {
        private static readonly object simulationLock = new();

        public static SeedReport Analyze(string seedInput, PathosContext? externalContext = null)
        {
            var ctx = externalContext ?? PathosContext.Instance;
            if (ctx == null || !ctx.IsReady)
            {
                throw new InvalidOperationException("尚未配置有效的 Pathos 游戏安装目录！请先点击右上角设置您的游戏目录。");
            }
            seedInput = seedInput.Trim();

            dynamic seed;
            if (seedInput.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                seed = ctx.CreateSeed(seedInput);
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(seedInput, @"\A\b[0-9a-fA-F]{1,8}\b\Z"))
            {
                // Hex string without '#', normalize to '#HEX'
                seed = ctx.CreateSeed("#" + seedInput);
            }
            else if (int.TryParse(seedInput, out int numericCode))
            {
                // Hex code formatted as decimal
                seed = ctx.CreateSeedFromCode(numericCode);
            }
            else
            {
                // Plain text word/phrase
                seed = ctx.CreateSeed(seedInput);
            }

            return AnalyzeSeed(seed, ctx);
        }

        private static dynamic SimulateDungeon(PathosContext ctx, dynamic seed, int seedCode, SeedReport report)
        {
            var swDungeon = Stopwatch.StartNew();
            dynamic engine = ctx.CreateEngine(seed);
            dynamic adventure = engine.Adventure;

            adventure.ChapterList.Clear();
            adventure.PlayerList.Clear();

            ctx.Reseed(Environment.TickCount);
            ctx.Reseed(seedCode);
            ctx.PrepareShuffle(adventure);
            ctx.NethackModule.Execute(engine.Generator);
            ctx.CommitShuffle(adventure);
            swDungeon.Stop();

            var swCache = Stopwatch.StartNew();
            try
            {
                ExtractIdentifiedItems(report, ctx);
                // MapService.CacheAdventure(report.SeedText, report.SeedCode, adventure);
            }
            finally
            {
                ctx.ReverseShuffle(adventure);
            }
            swCache.Stop();
            // Console.WriteLine($"[SIM-DETAIL] DungeonGen: {swDungeon.ElapsedMilliseconds} ms, ExtractId: {swCache.ElapsedMilliseconds} ms");

            return adventure;
        }

        public static SeedReport AnalyzeSeed(dynamic seed, PathosContext? externalContext = null)
        {
            var ctx = externalContext ?? PathosContext.Instance;
            if (ctx == null || !ctx.IsReady)
            {
                throw new InvalidOperationException("Pathos 引擎上下文未就绪");
            }

            int seedCode = (int)seed.Code;
            string seedText = (string)(seed.Text ?? ("#" + seedCode.ToString("X8")));

            var report = new SeedReport
            {
                SeedText = seedText,
                SeedCode = seedCode
            };

            var sw = Stopwatch.StartNew();

            dynamic adventure;
            // If externalContext is provided (e.g. from an isolated AssemblyLoadContext worker),
            // this worker thread owns its isolated engine instance and can run 100% in parallel without locks!
            if (externalContext != null)
            {
                adventure = SimulateDungeon(ctx, seed, seedCode, report);
            }
            else
            {
                lock (simulationLock)
                {
                    adventure = SimulateDungeon(ctx, seed, seedCode, report);
                }
            }

            sw.Stop();
            report.ElapsedMs = sw.ElapsedMilliseconds;

            var trans = ctx.Translations;
            var sokobanInfo = new SokobanInfo();

            // Elf Kingdom tracking
            int elfTownHighElves = 0;
            int elfPalaceHighElves = 0;
            int elfTotalHighElves = 0;
            int elfOrcs = 0;
            bool elfKingFound = false;
            bool elfKingCarriesArtifact = false;
            string elfArtifactName = "";
            bool elfSiteDetected = false;

            // 1. Scan Artifacts
            var seenAssets = new HashSet<object>();

            foreach (var map in (IEnumerable)adventure.World.Maps)
            {
                dynamic dm = map;
                string mapRawName = (string)(dm.Name ?? "");
                var loc = ResolveMapLocation(dm, trans);

                // Build a map of character inventory assets (both equipped and carried)
                var charAssetMap = new Dictionary<object, dynamic>();
                foreach (var character in (IEnumerable)dm.GetCharacters())
                {
                    dynamic dc = character;
                    if (dc.Inventory != null)
                    {
                        foreach (var a in (IEnumerable)dc.Inventory.GetPossessedAssets())
                        {
                            charAssetMap[a] = dc;
                        }
                    }
                }

                // Build stashMap (container inner assets) and squareMap (asset to square)
                var stashMap = new Dictionary<object, dynamic>();
                var squareMap = new Dictionary<object, dynamic>();

                foreach (var sq in (IEnumerable)dm.GetSquares())
                {
                    dynamic dsq = sq;
                    foreach (var a in (IEnumerable)dsq.GetAssets())
                    {
                        squareMap[a] = dsq;
                        dynamic da = a;
                        if (da.Container != null && da.Container.Stash != null)
                        {
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
                                    stashMap[inside] = da;
                                    squareMap[inside] = dsq;
                                }
                            }
                        }
                    }
                }

                void ProcessArtifactAsset(dynamic da)
                {
                    if (!seenAssets.Add(da)) return;

                    dynamic item = da.Item;
                    if (item == null || item.Grade == null || (string)item.Grade.Name != "artifact") return;

                    string itemName = (string)item.Name;
                    var art = new ArtifactInfo
                    {
                        EnglishName = itemName,
                        ChineseName = trans.TranslateItem(itemName),
                        Glyph = (string)(item.Glyph?.Name ?? item.Glyph?.ToString() ?? itemName),
                        Site = loc.Site,
                        SiteChinese = loc.SiteChinese,
                        SubArea = loc.SubArea,
                        LevelIndex = loc.LevelIndex,
                        MapRawName = mapRawName
                    };

                    if (charAssetMap.TryGetValue((object)da, out var carrier))
                    {
                        art.Possession = PossessionType.Carried;
                        art.HolderName = (string)(carrier.Name ?? carrier.Entity?.Name ?? "character");
                        art.HolderChinese = trans.TranslateEntity(art.HolderName);
                        if (carrier.Square != null)
                        {
                            art.X = carrier.Square.X;
                            art.Y = carrier.Square.Y;
                        }
                    }
                    else if (stashMap.TryGetValue((object)da, out var containerAsset))
                    {
                        art.Possession = PossessionType.Container;
                        art.HolderName = (string)(containerAsset.Item?.Name ?? "container");
                        art.HolderChinese = trans.TranslateItem(art.HolderName);
                        art.IsLocked = containerAsset.Container?.Locked ?? false;
                        art.IsTrapped = containerAsset.Container?.Trap != null;

                        if (squareMap.TryGetValue((object)da, out var dsq) && dsq != null)
                        {
                            art.X = dsq.X;
                            art.Y = dsq.Y;
                            if (dsq.Boulder != null)
                            {
                                art.CoverBlockName = (string)(dsq.Boulder.Block?.Name ?? "");
                                art.CoverBlockChinese = trans.TranslateBlock(art.CoverBlockName);
                            }
                        }
                    }
                    else
                    {
                        if (squareMap.TryGetValue((object)da, out var dsq) && dsq != null)
                        {
                            art.X = dsq.X;
                            art.Y = dsq.Y;
                            if (dsq.Boulder != null)
                            {
                                art.Possession = PossessionType.Covered;
                                art.CoverBlockName = (string)(dsq.Boulder.Block?.Name ?? "");
                                art.CoverBlockChinese = trans.TranslateBlock(art.CoverBlockName);
                            }
                            else
                            {
                                art.Possession = PossessionType.Ground;
                            }
                        }
                        else
                        {
                            art.Possession = PossessionType.Ground;
                        }
                    }

                    report.Artifacts.Add(art);
                }

                // Scan all assets on the map via WalkAssets
                foreach (var asset in (IEnumerable)dm.WalkAssets())
                {
                    ProcessArtifactAsset(asset);
                }

                // Safety fallback: ensure any character-possessed artifacts not visited by WalkAssets are recorded
                foreach (var kvp in charAssetMap)
                {
                    ProcessArtifactAsset(kvp.Key);
                }

                // 2. Scan Minetown
                string mapClean = mapRawName.Trim('_');
                if (mapRawName.Contains("Minetown"))
                {
                    var mt = new MinetownInfo
                    {
                        MapName = mapClean,
                        Site = loc.Site,
                        SiteChinese = loc.SiteChinese,
                        LevelIndex = loc.LevelIndex
                    };

                    int watchmen = 0;
                    int orcs = 0;
                    int shopkeepers = 0;

                    foreach (var c in (IEnumerable)dm.GetCharacters())
                    {
                        dynamic dc = c;
                        string ename = (string)(dc.Entity?.Name ?? "");
                        string name = (string)(dc.Name ?? "");
                        string combined = $"{name} {ename}".ToLower();
                        if (combined.Contains("watch")) watchmen++;
                        if (combined.Contains("orc")) orcs++;
                        if (combined.Contains("keeper") || combined.Contains("merchant")) shopkeepers++;
                    }

                    mt.WatchmenCount = watchmen;
                    mt.OrcCount = orcs;
                    mt.MerchantCount = shopkeepers;

                    if (watchmen == 0 && orcs > 0)
                    {
                        mt.IsOrcSiege = true;
                        mt.StatusDescription = "兽人屠城/攻城状态 (Orcish Massacre)";
                    }
                    else
                    {
                        mt.IsOrcSiege = false;
                        mt.StatusDescription = "秩序井然/守卫巡逻状态 (Peaceful / Guarded)";
                    }

                    // Check for water / lava
                    foreach (var sq in (IEnumerable)dm.GetSquares())
                    {
                        dynamic dsq = sq;
                        string floorName = (string)(dsq.Floor?.Ground?.Name ?? "");
                        if (floorName.Contains("water")) mt.HasWater = true;
                        if (floorName.Contains("lava")) mt.HasLava = true;
                        if (mt.HasWater && mt.HasLava) break;
                    }

                    report.Minetown = mt;
                }

                // 2.5 Scan Elf Kingdom Characters & State
                string rawMapSite = (string)(dm.Level?.Site?.Name ?? "");
                bool isElfMap = rawMapSite.Contains("Elf", StringComparison.OrdinalIgnoreCase) || 
                                mapClean.Contains("Elf", StringComparison.OrdinalIgnoreCase);

                if (isElfMap)
                {
                    elfSiteDetected = true;
                    bool isElfTown = mapClean.Contains("Town", StringComparison.OrdinalIgnoreCase);
                    bool isElfPalace = mapClean.Contains("Palace", StringComparison.OrdinalIgnoreCase);

                    foreach (var c in (IEnumerable)dm.GetCharacters())
                    {
                        dynamic dc = c;
                        string ename = (string)(dc.Entity?.Name ?? "");
                        string name = (string)(dc.Name ?? "");
                        string combined = $"{name} {ename}".ToLowerInvariant();

                        if (ename.Equals("high elf", StringComparison.OrdinalIgnoreCase))
                        {
                            elfTotalHighElves++;
                            if (isElfTown) elfTownHighElves++;
                            if (isElfPalace) elfPalaceHighElves++;
                        }

                        if (combined.Contains("orc"))
                        {
                            elfOrcs++;
                        }

                        if (ename.Equals("elf king", StringComparison.OrdinalIgnoreCase))
                        {
                            elfKingFound = true;
                            if (dc.Inventory != null)
                            {
                                foreach (var a in (IEnumerable)dc.Inventory.GetPossessedAssets())
                                {
                                    dynamic da = a;
                                    if (da?.Item != null && (bool)(da.Item.Grade?.Unique ?? false))
                                    {
                                        elfKingCarriesArtifact = true;
                                        string aName = (string)(da.Item.Name ?? "");
                                        elfArtifactName = trans.TranslateItem(aName);
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Scan Shops & Merchants
                var merchantPositions = new Dictionary<string, (int X, int Y)>();
                foreach (var c in (IEnumerable)dm.GetCharacters())
                {
                    dynamic dc = c;
                    string entityName = (string)(dc.Entity?.Name ?? "");
                    if (entityName.Contains("merchant") || entityName.Contains("keeper"))
                    {
                        string mName = (string)(dc.Name ?? entityName);
                        if (dc.Square != null)
                        {
                            merchantPositions[mName] = (dc.Square.X, dc.Square.Y);
                        }
                    }
                }

                var shopTriggers = new List<dynamic>();
                if (dm.Trigger != null) shopTriggers.Add(dm.Trigger);
                foreach (var zone in (IEnumerable)dm.Zones)
                {
                    dynamic dz = zone;
                    if (dz.Trigger != null) shopTriggers.Add(dz.Trigger);
                }

                foreach (var trig in shopTriggers)
                {
                    foreach (var sched in (IEnumerable)trig.Schedules)
                    {
                        dynamic ds = sched;
                        dynamic routine = ds.Routine;
                        if (routine?.Trick != null)
                        {
                            string tname = (string)routine.Trick.Name;
                            if (tname.StartsWith("visited "))
                            {
                                string shopType = tname.Substring("visited ".Length);
                                int sx = routine.Target?.X ?? 0;
                                int sy = routine.Target?.Y ?? 0;

                                // Find closest merchant if any
                                string nearestMerchant = "";
                                foreach (var (mName, (mx, my)) in merchantPositions)
                                {
                                    if (Math.Abs(mx - sx) <= 6 && Math.Abs(my - sy) <= 6)
                                    {
                                        nearestMerchant = mName;
                                        break;
                                    }
                                }

                                report.Shops.Add(new ShopInfo
                                {
                                    EnglishType = shopType,
                                    ChineseType = trans.TranslateShop(shopType),
                                    Site = loc.Site,
                                    SiteChinese = loc.SiteChinese,
                                    SubArea = loc.SubArea,
                                    LevelIndex = loc.LevelIndex,
                                    MerchantName = nearestMerchant,
                                    X = sx,
                                    Y = sy
                                });
                            }
                        }
                    }
                }

                // 4. Scan Sokoban
                if (mapRawName.Contains("Sokoban"))
                {
                    var floorMatch = System.Text.RegularExpressions.Regex.Match(mapRawName, @"Sokoban\D*(\d+)");
                    int sokoFloor = floorMatch.Success ? int.Parse(floorMatch.Groups[1].Value) : 0;

                    foreach (var sq in (IEnumerable)dm.GetSquares())
                    {
                        dynamic dsq = sq;

                        if (sokobanInfo.DungeonEntranceLevel == 0 && dsq.Passage != null && dsq.Passage.Destination != null)
                        {
                            string destMap = (string)(dsq.Passage.Destination.Map?.Name ?? "");
                            if (IsMainDungeonMap(destMap))
                            {
                                int lvl = ExtractDungeonFloor(destMap);
                                if (lvl > 0)
                                {
                                    sokobanInfo.DungeonEntranceLevel = lvl;
                                }
                            }
                        }

                        foreach (var a in (IEnumerable)dsq.GetAssets())
                        {
                            dynamic da = a;
                            dynamic item = da.Item;
                            if (item == null) continue;

                            string iName = (string)(item.Name ?? "");
                            string iType = item.Type?.ToString() ?? "";
                            string cn = trans.TranslateItem(iName);

                            var reward = new SokobanRewardInfo
                            {
                                EnglishName = iName,
                                ChineseName = cn,
                                Glyph = (string)(item.Glyph?.Name ?? item.Glyph?.ToString() ?? iName),
                                ItemType = iType,
                                Floor = sokoFloor,
                                X = dsq.X,
                                Y = dsq.Y
                            };

                            if (sokoFloor == 4)
                            {
                                if (iType == "Bag" && sokobanInfo.FinalBag == null)
                                    sokobanInfo.FinalBag = reward;
                                else if (iType == "Cloak" && sokobanInfo.FinalCloak == null)
                                    sokobanInfo.FinalCloak = reward;
                                else if (iType == "Amulet" && sokobanInfo.FinalAmulet == null)
                                    sokobanInfo.FinalAmulet = reward;
                                else if (iType == "Ring" && sokobanInfo.FinalRing == null)
                                    sokobanInfo.FinalRing = reward;
                                else if (iType == "Wand" && sokobanInfo.FinalWand == null)
                                    sokobanInfo.FinalWand = reward;
                            }
                            else if (sokoFloor >= 1 && sokoFloor <= 3)
                            {
                                if (iType == "Ring" || iType == "Wand")
                                {
                                    sokobanInfo.FloorRewards.Add(reward);
                                }
                            }
                        }
                    }
                }
                else if (IsMainDungeonMap(mapRawName))
                {
                    int dFloor = ExtractDungeonFloor(mapRawName);

                    foreach (var sq in (IEnumerable)dm.GetSquares())
                    {
                        dynamic dsq = sq;
                        if (dsq.Passage != null && dsq.Passage.Destination != null)
                        {
                            string destMap = (string)(dsq.Passage.Destination.Map?.Name ?? "");
                            if (!IsMainDungeonMap(destMap))
                            {
                                string rawDestSite = (string)(dsq.Passage.Destination.Map?.Level?.Site?.Name ?? destMap);
                                string destSite = rawDestSite.Trim('_');
                                string portalName = (string)(dsq.Passage.Portal?.Name ?? "portal");

                                if (destSite.Equals("Sokoban", StringComparison.OrdinalIgnoreCase) && sokobanInfo.DungeonEntranceLevel == 0)
                                {
                                    sokobanInfo.DungeonEntranceLevel = dFloor;
                                }

                                if (!report.BranchEntrances.Any(b => b.DungeonLevel == dFloor && b.BranchName.Equals(destSite, StringComparison.OrdinalIgnoreCase)))
                                {
                                    report.BranchEntrances.Add(new BranchEntranceInfo
                                    {
                                        BranchName = destSite,
                                        BranchChinese = trans.TranslateSite(destSite),
                                        DungeonLevel = dFloor,
                                        PortalName = portalName,
                                        PortalChinese = trans.TranslatePortal(portalName),
                                        X = dsq.X,
                                        Y = dsq.Y,
                                        DestinationMap = destMap.Trim('_')
                                    });
                                }
                            }
                        }
                    }
                }
            }

            report.BranchEntrances = report.BranchEntrances.OrderBy(b => b.DungeonLevel).ToList();

            if (elfSiteDetected || elfKingFound || elfTotalHighElves > 0)
            {
                bool isElfMassacre = (elfTownHighElves == 0 && elfPalaceHighElves > 0) || elfKingCarriesArtifact;
                var elfEntrance = report.BranchEntrances.FirstOrDefault(b => b.BranchName.Equals("Elf Kingdom", StringComparison.OrdinalIgnoreCase));

                report.ElfKingdom = new ElfKingdomInfo
                {
                    MapName = "Elf Forest",
                    Site = "Elf Kingdom",
                    SiteChinese = trans.TranslateSite("Elf Kingdom"),
                    DungeonEntranceLevel = elfEntrance?.DungeonLevel ?? 0,
                    IsMassacre = isElfMassacre,
                    StatusDescription = isElfMassacre
                        ? "屠城惨案/暴动敌对状态 (Elf Massacre & Rebellion)"
                        : "盛世繁荣/和平友好状态 (Prosperous & Peaceful)",
                    TotalHighElfCount = elfTotalHighElves,
                    TownHighElfCount = elfTownHighElves,
                    PalaceHighElfCount = elfPalaceHighElves,
                    OrcCount = elfOrcs,
                    KingState = isElfMassacre
                        ? (!string.IsNullOrEmpty(elfArtifactName) ? $"佩戴专属神器【{elfArtifactName}】全副武装坐镇迎战" : "全副武装高度戒备")
                        : "坐镇王座，友好中立",
                    ArtifactState = isElfMassacre
                        ? (!string.IsNullOrEmpty(elfArtifactName) ? $"由精灵王亲自装备武装携带【{elfArtifactName}】" : "由精灵王随身武装携带")
                        : "置于王宫宴会大厅长桌之上陈列供奉"
                };
            }

            if (sokobanInfo.FinalBag != null || sokobanInfo.FinalCloak != null || sokobanInfo.FinalAmulet != null || sokobanInfo.DungeonEntranceLevel > 0)
            {
                report.Sokoban = sokobanInfo;
            }

            // 5. Convert Branch Levels to Equivalent Main Dungeon Depth
            var branchEntranceMap = report.BranchEntrances
                .GroupBy(b => b.BranchName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().DungeonLevel, StringComparer.OrdinalIgnoreCase);

            int minesEntranceLevel = branchEntranceMap.TryGetValue("Mines", out int mEntry) ? mEntry : 3;

            foreach (var art in report.Artifacts)
            {
                if (art.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase))
                {
                    art.DungeonLevel = art.LevelIndex;
                }
                else if (art.Site.Equals("Underdeep", StringComparison.OrdinalIgnoreCase))
                {
                    art.DungeonLevel = minesEntranceLevel + 4; // Starts after Mines 5
                }
                else if (branchEntranceMap.TryGetValue(art.Site, out int entryLevel))
                {
                    art.DungeonLevel = entryLevel;
                }
                else
                {
                    art.DungeonLevel = art.LevelIndex;
                }
            }

            foreach (var shop in report.Shops)
            {
                if (shop.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase))
                {
                    shop.DungeonLevel = shop.LevelIndex;
                }
                else if (shop.Site.Equals("Underdeep", StringComparison.OrdinalIgnoreCase))
                {
                    shop.DungeonLevel = minesEntranceLevel + 4;
                }
                else if (branchEntranceMap.TryGetValue(shop.Site, out int entryLevel))
                {
                    shop.DungeonLevel = entryLevel;
                }
                else
                {
                    shop.DungeonLevel = shop.LevelIndex;
                }
            }

            return report;
        }

        private static bool IsMainDungeonMap(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return false;
            return rawName.Contains("level//depth", StringComparison.OrdinalIgnoreCase) ||
                   System.Text.RegularExpressions.Regex.IsMatch(rawName, @"^(?:_?level//depth|_?Dungeon)\D*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static int ExtractDungeonFloor(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return 0;
            var m = System.Text.RegularExpressions.Regex.Match(rawName, @"(?:level//depth|Dungeon)\D*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out int lvl) ? lvl : 0;
        }

        private static MapLocation ResolveMapLocation(dynamic dm, TranslationManager trans)
        {
            string rawSite = (string)(dm.Level?.Site?.Name ?? dm.Name ?? "Special");
            string siteClean = rawSite.Trim('_');
            int rawLevelIndex = dm.Level?.Index ?? 0;
            string mapRawName = (string)(dm.Name ?? "");
            string mapClean = mapRawName.Trim('_');

            if (siteClean.Equals("Mines", StringComparison.OrdinalIgnoreCase))
            {
                // Case 1: Minetown
                if (mapClean.Contains("Minetown", StringComparison.OrdinalIgnoreCase))
                {
                    return new MapLocation("Mines", trans.TranslateSite("Mines"), 1, trans.TranslateSite("Minetown"));
                }

                // Case 2: Mines levels 1 to 5 (mapRawName is "_Mines_ 1" ... "_Mines_ 5")
                var minesMatch = System.Text.RegularExpressions.Regex.Match(mapRawName, @"Mines\D*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (minesMatch.Success && int.TryParse(minesMatch.Groups[1].Value, out int floorNum))
                {
                    return new MapLocation("Mines", trans.TranslateSite("Mines"), floorNum, "");
                }

                // Case 3: Underdeep levels (below Mines 5)
                // Maps in Underdeep.Quest: Underpeaks (1), Underlimits (2), Undertown (3), Underbarracks (4), Deepwilds (5), Deepruins (6), Deephall (7)
                var underdeepMaps = new[]
                {
                    "Underpeaks",     // 地城通路
                    "Underlimits",    // 地城边境
                    "Undertown",      // 地下城
                    "Underbarracks",  // 地城兵营
                    "Deepwilds",      // 地下荒野
                    "Deepruins",      // 地下废墟
                    "Deephall"        // 地下大厅
                };

                int underIdx = Array.FindIndex(underdeepMaps, m => m.Equals(mapClean, StringComparison.OrdinalIgnoreCase));
                int floor = underIdx >= 0 ? underIdx + 1 : Math.Max(1, rawLevelIndex - 6);
                string subArea = trans.TranslateSite(mapClean);

                return new MapLocation("Underdeep", "地城", floor, subArea);
            }

            if (siteClean.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) || IsMainDungeonMap(mapRawName))
            {
                int floor = ExtractDungeonFloor(mapRawName);
                if (floor == 0) floor = rawLevelIndex;

                var match = System.Text.RegularExpressions.Regex.Match(mapRawName, @"(?:level//depth|Dungeon)\D*\d+(.*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string suffix = match.Success ? match.Groups[1].Value.Trim().Trim('_').Trim() : "";

                string subArea = suffix.ToLowerInvariant() switch
                {
                    "attic" => "阁楼",
                    "cellar" => "地窖",
                    "trove" => "藏宝密室",
                    "caves" => "洞穴",
                    _ => string.IsNullOrEmpty(suffix) ? "" : trans.TranslateSite(suffix)
                };

                return new MapLocation("Dungeon", "主地牢", floor, subArea);
            }

            return new MapLocation(siteClean, trans.TranslateSite(siteClean), rawLevelIndex, "");
        }

        private static void ExtractIdentifiedItems(SeedReport report, PathosContext ctx)
        {
            var trans = ctx.Translations;
            var list = new List<IdentifiedItemInfo>();

            foreach (var it in (IEnumerable)ctx.Manifest.Items.List)
            {
                dynamic item = it;
                if (item.Series != null)
                {
                    string seriesEn = (string)item.Series.Name;
                    string seriesCn = seriesEn switch
                    {
                        "potion" => "药水",
                        "scroll" => "卷轴",
                        "ring" => "戒指",
                        "wand" => "魔杖",
                        "amulet" => "护符",
                        "book" => "技能书",
                        "boots" => "靴子",
                        "cloak" => "斗篷",
                        "helm" => "头盔",
                        "gloves" => "手套",
                        "robes" => "长袍",
                        "staff" => "手杖",
                        "earrings" => "耳环",
                        _ => seriesEn
                    };

                    string trueEn = (string)item.Name;
                    string trueCn = trans.TranslateItem(trueEn);
                    string appEn = (string)(item.Appearance?.Name ?? "");
                    string appCn = trans.TranslateAppearance(appEn);
                    string glyph = (string)(item.Glyph?.Name ?? item.Glyph?.ToString() ?? item.Appearance?.Name ?? item.Name);

                    list.Add(new IdentifiedItemInfo
                    {
                        Series = seriesEn,
                        SeriesChinese = seriesCn,
                        TrueNameEn = trueEn,
                        TrueNameCn = trueCn,
                        ApparentNameEn = appEn,
                        ApparentNameCn = appCn,
                        Glyph = glyph
                    });
                }
            }

            int GetSeriesPriority(string s) => s switch
            {
                "potion" => 1,
                "scroll" => 2,
                "wand" => 3,
                "ring" => 4,
                "amulet" => 5,
                "book" => 6,
                "cloak" => 7,
                "boots" => 8,
                "helm" => 9,
                "gloves" => 10,
                "robes" => 11,
                "staff" => 12,
                "earrings" => 13,
                _ => 20
            };

            report.IdentifiedItems = list
                .OrderBy(x => GetSeriesPriority(x.Series))
                .ThenBy(x => x.TrueNameCn)
                .ToList();
        }
    }
}
