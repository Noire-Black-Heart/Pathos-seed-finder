using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PathosSeedFinder.Core
{
    public readonly struct BranchIndices
    {
        public int Mines { get; init; }
        public int Sokoban { get; init; }
        public int Fort { get; init; }
        public int Labyrinth { get; init; }
        public int Chambers { get; init; }
        public int Lair { get; init; }
        public int Kingdom { get; init; }
        public int Tower { get; init; }
        public int Market { get; init; }
        public int Abyss { get; init; }
    }

    internal enum RuleEvalState
    {
        Undecided = 0,
        Matched = 1,
        Failed = 2
    }

    internal sealed class CompiledRule
    {
        public ConditionRule Source { get; init; } = null!;
        public string Type { get; init; } = "";
        public bool IsOptional { get; init; }

        // Artifact / Shop shared pre-compiled fields
        public string ReqNameRaw { get; init; } = "";
        public string ReqNameEn { get; init; } = "";
        public string ReqSiteRaw { get; init; } = "";
        public bool IsAnySite { get; init; }
        public bool IsDungeonSiteOnly { get; init; }
        public int? MaxLevel { get; init; }
        public bool AllowCarried { get; init; } = true;

        // Specific rule fields
        public bool WantMinetownOrc { get; init; }
        public bool WantMinetownNormal { get; init; }
        public bool WantMinetownAny { get; init; }

        public bool WantElfMassacre { get; init; }
        public bool WantElfNormal { get; init; }
        public bool WantElfAny { get; init; }

        public string ReqSokobanPrizeRaw { get; init; } = "";
        public string ReqSokobanPrizeEn { get; init; } = "";
        public int? SokobanLevel { get; init; }

        public bool HasShopFilter { get; init; }
        public int? MinShops { get; init; }
        public int? MinArtifacts { get; init; }

        // Pre-resolved candidate site flags when ReqSiteRaw is a specific non-dungeon site
        public bool SiteCouldMatchDungeon { get; init; }
        public bool SiteCouldMatchMines { get; init; }
        public bool SiteCouldMatchUnderdeep { get; init; }
        public bool SiteCouldMatchSokoban { get; init; }
        public bool SiteCouldMatchFort { get; init; }
        public bool SiteCouldMatchLabyrinth { get; init; }
        public bool SiteCouldMatchChambers { get; init; }
        public bool SiteCouldMatchLair { get; init; }
        public bool SiteCouldMatchKingdom { get; init; }
        public bool SiteCouldMatchTower { get; init; }
        public bool SiteCouldMatchMarket { get; init; }
        public bool SiteCouldMatchAbyss { get; init; }
        public bool SiteIsUnknown { get; init; }

        public int ComputeStopFloor(in BranchIndices b)
        {
            switch (Type)
            {
                case "minetown":
                    return b.Mines;

                case "elf":
                    return b.Kingdom;

                case "sokoban":
                    if (SokobanLevel.HasValue && SokobanLevel.Value != b.Sokoban)
                    {
                        return 0; // Pre-floor-1 instant reject!
                    }
                    return b.Sokoban;

                case "artifact_count":
                    if (!MinArtifacts.HasValue || MinArtifacts.Value <= 0) return 0;
                    return 40;

                case "artifact":
                    if (IsAnySite || IsDungeonSiteOnly)
                    {
                        if (MaxLevel.HasValue)
                        {
                            if (MaxLevel.Value < 1) return 0;
                            return Math.Min(40, MaxLevel.Value);
                        }
                        return 40;
                    }
                    return ComputeBranchStopFloor(b, isShop: false);

                case "shop":
                    if (!HasShopFilter)
                    {
                        if (!MinShops.HasValue || MinShops.Value <= 0) return 0;
                        return 40;
                    }
                    if (IsAnySite || IsDungeonSiteOnly)
                    {
                        if (MaxLevel.HasValue)
                        {
                            if (MaxLevel.Value < 1) return 0;
                            return Math.Min(40, MaxLevel.Value);
                        }
                        return 40;
                    }
                    return ComputeBranchStopFloor(b, isShop: true);

                default:
                    return 0;
            }
        }

        private int ComputeBranchStopFloor(in BranchIndices b, bool isShop)
        {
            if (SiteIsUnknown)
            {
                return 40;
            }

            int maxFloor = 0;
            void Consider(bool matches, int genFloor, int dungeonDepth)
            {
                if (!matches) return;
                // For shop rules, MaxLevel always checks DungeonLevel (when > 0), so branches deeper than MaxLevel cannot match.
                if (isShop && MaxLevel.HasValue && dungeonDepth > MaxLevel.Value) return;
                if (genFloor > maxFloor) maxFloor = genFloor;
            }

            Consider(SiteCouldMatchDungeon, MaxLevel.HasValue ? Math.Min(40, Math.Max(0, MaxLevel.Value)) : 40, 1);
            Consider(SiteCouldMatchMines, b.Mines, b.Mines);
            Consider(SiteCouldMatchUnderdeep, b.Mines, b.Mines + 4);
            Consider(SiteCouldMatchSokoban, b.Sokoban, b.Sokoban);
            Consider(SiteCouldMatchFort, b.Fort, b.Fort);
            Consider(SiteCouldMatchLabyrinth, b.Labyrinth, b.Labyrinth);
            Consider(SiteCouldMatchChambers, b.Chambers, b.Chambers);
            Consider(SiteCouldMatchLair, b.Lair, b.Lair);
            Consider(SiteCouldMatchKingdom, b.Kingdom, b.Kingdom);
            Consider(SiteCouldMatchTower, b.Tower, b.Tower);
            Consider(SiteCouldMatchMarket, b.Market, b.Market);
            Consider(SiteCouldMatchAbyss, b.Abyss, b.Abyss);

            return maxFloor;
        }
    }

    public sealed class CompiledFilterPlan
    {
        public FilterCriteria Criteria { get; }
        public bool IsAnyMode { get; }
        internal List<CompiledRule> Rules { get; }
        public bool HasRules => Rules.Count > 0;
        public bool HasOptionalRules { get; }

        private static readonly string[] UnderdeepSubNames =
        {
            "Underpeaks", "Underlimits", "Undertown", "Underbarracks", "Deepwilds", "Deepruins", "Deephall"
        };

        private static readonly string[] DungeonSubNames =
        {
            "attic", "阁楼", "cellar", "地窖", "trove", "藏宝密室", "caves", "洞穴"
        };

        private CompiledFilterPlan(FilterCriteria criteria, TranslationManager trans)
        {
            Criteria = criteria;
            SeedFilter.EnsureRules(criteria);
            IsAnyMode = string.Equals(criteria.MatchMode, "any", StringComparison.OrdinalIgnoreCase);
            Rules = new List<CompiledRule>(criteria.Rules.Count);

            foreach (var r in criteria.Rules)
            {
                string type = (r.Type ?? "artifact").ToLowerInvariant();
                bool isOpt = !IsAnyMode && string.Equals(r.LogicRole, "optional", StringComparison.OrdinalIgnoreCase);

                string reqSite = (r.Site ?? "").Trim();
                bool isAnySite = string.IsNullOrEmpty(reqSite);
                bool isDungeonSiteOnly = !isAnySite && (
                    reqSite.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                    reqSite.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                    reqSite.Equals("主地牢", StringComparison.OrdinalIgnoreCase));

                bool mDungeon = false, mMines = false, mUnderdeep = false, mSokoban = false,
                     mFort = false, mLabyrinth = false, mChambers = false, mLair = false,
                     mKingdom = false, mTower = false, mMarket = false, mAbyss = false, mUnknown = false;

                if (!isAnySite && !isDungeonSiteOnly)
                {
                    bool ContainsSite(string en, params string[] extraSubs)
                    {
                        string cn = trans.TranslateSite(en);
                        if (en.Contains(reqSite, StringComparison.OrdinalIgnoreCase) ||
                            cn.Contains(reqSite, StringComparison.OrdinalIgnoreCase))
                            return true;
                        foreach (var sub in extraSubs)
                        {
                            if (sub.Contains(reqSite, StringComparison.OrdinalIgnoreCase) ||
                                trans.TranslateSite(sub).Contains(reqSite, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                        return false;
                    }

                    mDungeon = ContainsSite("Dungeon", DungeonSubNames);
                    mMines = ContainsSite("Mines", "Minetown");
                    mUnderdeep = ContainsSite("Underdeep", UnderdeepSubNames);
                    mSokoban = ContainsSite("Sokoban");
                    mFort = ContainsSite("Fort Ludios", "Ludios Cache");
                    mLabyrinth = ContainsSite("Labyrinth");
                    mChambers = ContainsSite("Lost Chambers");
                    mLair = ContainsSite("Medusa Lair");
                    mKingdom = ContainsSite("Elf Kingdom", "Elf Forest", "Elf Town", "Elf Palace");
                    mTower = ContainsSite("Lich Tower");
                    mMarket = ContainsSite("Black Market");
                    mAbyss = ContainsSite("Abyss");

                    if (!(mDungeon || mMines || mUnderdeep || mSokoban || mFort || mLabyrinth ||
                          mChambers || mLair || mKingdom || mTower || mMarket || mAbyss))
                    {
                        mUnknown = true;
                    }
                }

                string mtState = (r.MinetownState ?? "").Trim().ToLowerInvariant();
                bool mtOrc = mtState.Contains("orc") || mtState.Contains("兽人") || mtState.Contains("攻城") || mtState.Contains("屠城");
                bool mtNorm = !mtOrc && (mtState.Contains("normal") || mtState.Contains("正常") || mtState.Contains("和平") || mtState.Contains("守卫"));

                string elfState = (r.ElfKingdomState ?? "").Trim().ToLowerInvariant();
                bool elfMass = elfState.Contains("massacre") || elfState.Contains("orc") || elfState.Contains("屠城") || elfState.Contains("惨案") || elfState.Contains("暴动") || elfState.Contains("敌对");
                bool elfNorm = !elfMass && (elfState.Contains("normal") || elfState.Contains("peaceful") || elfState.Contains("正常") || elfState.Contains("和平") || elfState.Contains("盛世") || elfState.Contains("繁荣"));

                string reqArt = (r.ArtifactName ?? "").Trim();
                string reqShop = (r.ShopType ?? "").Trim();
                string reqSoko = (r.SokobanPrize ?? "").Trim();

                var compiled = new CompiledRule
                {
                    Source = r,
                    Type = type,
                    IsOptional = isOpt,
                    ReqNameRaw = type == "shop" ? reqShop : reqArt,
                    ReqNameEn = type == "shop"
                        ? (string.IsNullOrEmpty(reqShop) ? "" : trans.ResolveShopEnglish(reqShop))
                        : (string.IsNullOrEmpty(reqArt) ? "" : trans.ResolveItemEnglish(reqArt)),
                    ReqSiteRaw = reqSite,
                    IsAnySite = isAnySite,
                    IsDungeonSiteOnly = isDungeonSiteOnly,
                    MaxLevel = r.MaxLevel,
                    AllowCarried = r.AllowCarried,

                    WantMinetownOrc = mtOrc,
                    WantMinetownNormal = mtNorm,
                    WantMinetownAny = !mtOrc && !mtNorm,

                    WantElfMassacre = elfMass,
                    WantElfNormal = elfNorm,
                    WantElfAny = !elfMass && !elfNorm,

                    ReqSokobanPrizeRaw = reqSoko,
                    ReqSokobanPrizeEn = string.IsNullOrEmpty(reqSoko) ? "" : trans.ResolveItemEnglish(reqSoko),
                    SokobanLevel = r.SokobanLevel,

                    HasShopFilter = !string.IsNullOrEmpty(reqShop) || r.MaxLevel.HasValue || !isAnySite,
                    MinShops = r.MinShops,
                    MinArtifacts = r.MinArtifacts,

                    SiteCouldMatchDungeon = mDungeon,
                    SiteCouldMatchMines = mMines,
                    SiteCouldMatchUnderdeep = mUnderdeep,
                    SiteCouldMatchSokoban = mSokoban,
                    SiteCouldMatchFort = mFort,
                    SiteCouldMatchLabyrinth = mLabyrinth,
                    SiteCouldMatchChambers = mChambers,
                    SiteCouldMatchLair = mLair,
                    SiteCouldMatchKingdom = mKingdom,
                    SiteCouldMatchTower = mTower,
                    SiteCouldMatchMarket = mMarket,
                    SiteCouldMatchAbyss = mAbyss,
                    SiteIsUnknown = mUnknown
                };

                Rules.Add(compiled);
            }

            HasOptionalRules = !IsAnyMode && Rules.Any(r => r.IsOptional);
        }

        public static CompiledFilterPlan Compile(FilterCriteria criteria, TranslationManager trans)
        {
            return new CompiledFilterPlan(criteria, trans);
        }
    }

    /// <summary>
    /// 极速按需分层生成与短路评估引擎：
    /// 1. 精确复现 NetHackMaker.Create() 的逐层随机流与生成步骤，支持任意层安全提前中断；
    /// 2. 只针对当前尚未决定的过滤规则做最小开销按需提取，不构建完整 SeedReport；
    /// 3. 对极少数命中候选种子统一走官方 SeedAnalyzer.Analyze + SeedFilter.Matches 终审，确保零误报、零层数穿透。
    /// </summary>
    public static class SteppedSeedEvaluator
    {
        private static readonly Regex SokobanFloorRegex = new(@"Sokoban\D*(\d+)", RegexOptions.Compiled);

        public static BranchIndices RollBranchIndices(Func<int, int> nextNumber)
        {
            int mines = 1 + (nextNumber(3) + 1);
            int sokoban = mines + (nextNumber(4) + 1);
            int fort = Math.Max(sokoban + 1, 7 + (nextNumber(6) + 1));
            int labyrinth = Math.Max(fort + 1, 10 + (nextNumber(6) + 1));
            int chambers = Math.Max(labyrinth + 1, 13 + (nextNumber(6) + 1));
            int lair = 20 + (nextNumber(5) + 1) - 1;
            int kingdom = lair + (nextNumber(30 - lair - 1) + 1);
            int tower = 30 + (nextNumber(5) + 1) - 1;
            int market = tower + (nextNumber(5) + 1);
            return new BranchIndices
            {
                Mines = mines,
                Sokoban = sokoban,
                Fort = fort,
                Labyrinth = labyrinth,
                Chambers = chambers,
                Lair = lair,
                Kingdom = kingdom,
                Tower = tower,
                Market = market,
                Abyss = 40
            };
        }

        private static object? PickRandomOrNull(List<object> list, Func<int, int> nextNumber)
        {
            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];
            return list[nextNumber(list.Count)];
        }

        /// <summary>
        /// 仅用于验证逐层步进生成与原版 NethackModule.Execute 的 100% 比特级等价性。
        /// </summary>
        internal static dynamic SimulateSteppedFullForTest(PathosContext ctx, dynamic seed, int seedCode)
        {
            var nextNum = ctx.NextNumberFunc!;
            dynamic engine = ctx.CreateEngine(seed);
            dynamic adventure = engine.Adventure;
            adventure.ChapterList.Clear();
            adventure.PlayerList.Clear();

            ctx.Reseed(Environment.TickCount);
            ctx.Reseed(seedCode);
            ctx.PrepareShuffle(adventure);

            dynamic generator = engine.Generator;
            object maker = ctx.CreateNetHackMaker(generator);
            string dungeonTerm = (string)generator.EscapedModuleTerm("Dungeon");
            dynamic site = adventure.World.AddSite(dungeonTerm);

            var branches = RollBranchIndices(nextNum);
            ctx.SetMakerIndices(maker, branches);

            dynamic? prevDownSquare = null;
            for (int i = 1; i <= 40; i++)
            {
                prevDownSquare = GenerateSingleFloor(ctx, maker, generator, site, dungeonTerm, i, 40, prevDownSquare, nextNum);
            }

            ctx.CommitShuffle(adventure);
            ctx.ReverseShuffle(adventure);
            return adventure;
        }

        private static dynamic? GenerateSingleFloor(
            PathosContext ctx,
            object maker,
            dynamic generator,
            dynamic site,
            string dungeonTerm,
            int i,
            int totalFloors,
            dynamic? prevDownSquare,
            Func<int, int> nextNum)
        {
            string name = dungeonTerm + " " + i;
            int mapSize = i < 30 ? 30 : (i < 40 ? 42 : 60);
            dynamic level = site.AddLevel(i, site.World.AddMap(name, mapSize, mapSize));
            level.Map.SetDifficulty(i);
            object dungeonStructure = ctx.InvokeCreateLevel(maker, i, level);

            dynamic levelUpPortal = (i >= 20 && i < 30) ? ctx.ClayStaircaseUp! : ctx.StoneStaircaseUp!;
            dynamic levelDownPortal = (i >= 20 && i < 30) ? ctx.ClayStaircaseDown! : ctx.StoneStaircaseDown!;

            // Exact replication of NetHackMaker.Create() lines 768-810
            var candidateRooms = new List<object>();
            foreach (var roomObj in ctx.GetStructureRooms(dungeonStructure))
            {
                if (ctx.IsRoomIsolated(roomObj)) continue;
                bool hasPortalSquare = false;
                foreach (var sq in ctx.GetRoomFloorSquares(roomObj))
                {
                    if ((bool)generator.CanPlacePortal((dynamic)sq))
                    {
                        hasPortalSquare = true;
                        break;
                    }
                }
                if (hasPortalSquare)
                {
                    candidateRooms.Add(roomObj);
                }
            }

            object? chosenUpRoom = PickRandomOrNull(candidateRooms, nextNum);
            object? square2 = null;
            if (chosenUpRoom != null)
            {
                var upSquares = new List<object>();
                foreach (var sq in ctx.GetRoomFloorSquares(chosenUpRoom))
                {
                    if ((bool)generator.CanPlacePortal((dynamic)sq)) upSquares.Add(sq);
                }
                square2 = PickRandomOrNull(upSquares, nextNum);
            }
            if (square2 == null)
            {
                var mapSquares = new List<object>();
                foreach (var sq in (IEnumerable)level.Map.GetSquares())
                {
                    if ((bool)generator.CanPlacePortal((dynamic)sq)) mapSquares.Add(sq);
                }
                square2 = PickRandomOrNull(mapSquares, nextNum);
            }
            if (square2 == null)
            {
                throw new Exception("UpSquare not found on: " + (string)level.Map.Name);
            }

            if (prevDownSquare == null)
            {
                generator.PlacePassage((dynamic)square2, levelUpPortal, null);
                generator.StartSquare((dynamic)square2);
            }
            else
            {
                generator.PlacePassage((dynamic)square2, levelUpPortal, prevDownSquare);
                generator.PlacePassage(prevDownSquare, levelDownPortal, (dynamic)square2);
            }

            level.Map.AddFullArea();

            object? square3;
            if (i == totalFloors)
            {
                square3 = level.DownSquare;
            }
            else
            {
                object? chosenDownRoom = null;
                if (candidateRooms.Count > 0)
                {
                    var otherRooms = new List<object>(Math.Max(0, candidateRooms.Count - 1));
                    foreach (var r in candidateRooms)
                    {
                        if (!ReferenceEquals(r, chosenUpRoom))
                        {
                            otherRooms.Add(r);
                        }
                    }
                    chosenDownRoom = PickRandomOrNull(otherRooms, nextNum) ?? chosenUpRoom;
                }

                square3 = null;
                if (chosenDownRoom != null)
                {
                    var downSquares = new List<object>();
                    foreach (var sq in ctx.GetRoomFloorSquares(chosenDownRoom))
                    {
                        if ((bool)generator.CanPlacePortal((dynamic)sq)) downSquares.Add(sq);
                    }
                    square3 = PickRandomOrNull(downSquares, nextNum);
                }
                if (square3 == null)
                {
                    var mapSquares = new List<object>();
                    foreach (var sq in (IEnumerable)level.Map.GetSquares())
                    {
                        if ((bool)generator.CanPlacePortal((dynamic)sq)) mapSquares.Add(sq);
                    }
                    square3 = PickRandomOrNull(mapSquares, nextNum);
                }
                if (square3 == null)
                {
                    throw new Exception("DownSquare not found on: " + (string)level.Map.Name);
                }
            }

            level.SetTransitions((dynamic)square2, (dynamic?)square3);
            return square3;
        }

        /// <summary>
        /// 快速两阶段漏斗匹配：
        /// 阶段 A：分层步进生成 + 按需局部扫描 + 规则级短路剪枝；
        /// 阶段 B：若候选命中，则调用原版全量 SeedAnalyzer.Analyze + SeedFilter.Matches 生成最终报告并终审。
        /// </summary>
        public static SeedReport? TryMatchSeed(string seedInput, PathosContext ctx, CompiledFilterPlan plan)
        {
            // No rules or stepped generation unavailable -> direct full path
            if (!plan.HasRules || !ctx.SupportsSteppedGeneration)
            {
                var directReport = SeedAnalyzer.Analyze(seedInput, ctx);
                return SeedFilter.Matches(directReport, plan.Criteria) ? directReport : null;
            }

            dynamic seed = SeedAnalyzer.ParseSeed(seedInput, ctx);
            int seedCode = (int)seed.Code;
            var nextNum = ctx.NextNumberFunc!;
            var trans = ctx.Translations;

            dynamic engine = ctx.CreateEngine(seed);
            dynamic adventure = engine.Adventure;
            adventure.ChapterList.Clear();
            adventure.PlayerList.Clear();

            ctx.Reseed(Environment.TickCount);
            ctx.Reseed(seedCode);
            ctx.PrepareShuffle(adventure);

            dynamic generator = engine.Generator;
            object maker = ctx.CreateNetHackMaker(generator);
            string dungeonTerm = (string)generator.EscapedModuleTerm("Dungeon");
            dynamic site = adventure.World.AddSite(dungeonTerm);

            // Roll all 10 branch entrance floors BEFORE generating Floor 1!
            var branches = RollBranchIndices(nextNum);
            ctx.SetMakerIndices(maker, branches);

            int ruleCount = plan.Rules.Count;
            var states = new RuleEvalState[ruleCount];
            var stopFloors = new int[ruleCount];

            // Stage 0: Pre-floor-1 evaluation & stop-floor computation
            for (int r = 0; r < ruleCount; r++)
            {
                var rule = plan.Rules[r];
                int sf = rule.ComputeStopFloor(branches);
                stopFloors[r] = sf;

                if (rule.Type == "artifact_count" && (!rule.MinArtifacts.HasValue || rule.MinArtifacts.Value <= 0))
                {
                    states[r] = RuleEvalState.Matched;
                }
                else if (rule.Type == "shop" && !rule.HasShopFilter && (!rule.MinShops.HasValue || rule.MinShops.Value <= 0))
                {
                    states[r] = RuleEvalState.Matched;
                }
                else if (rule.Type != "artifact" && rule.Type != "minetown" && rule.Type != "elf" &&
                         rule.Type != "sokoban" && rule.Type != "shop" && rule.Type != "artifact_count")
                {
                    states[r] = RuleEvalState.Matched;
                }
                else if (sf <= 0)
                {
                    states[r] = RuleEvalState.Failed;
                }
                else
                {
                    states[r] = RuleEvalState.Undecided;
                }
            }

            var overall = EvaluateOverall(plan, states);
            if (overall == RuleEvalState.Failed)
            {
                return null;
            }

            if (overall == RuleEvalState.Undecided)
            {
                // Tracking state across floors ONLY when needed
                HashSet<object>? seenArtifacts = null;
                int totalArtifacts = 0;
                int totalShops = 0;

                // Elf tracking (only accumulated on Kingdom floor)
                bool elfSiteDetected = false;
                bool elfKingFound = false;
                bool elfKingCarriesArtifact = false;
                int elfTownHighElves = 0;
                int elfPalaceHighElves = 0;
                int elfTotalHighElves = 0;

                // Sokoban tracking (only accumulated on Sokoban floor)
                bool sokobanExists = false;
                SokobanRewardInfo? sokoFinalBag = null;
                SokobanRewardInfo? sokoFinalCloak = null;
                SokobanRewardInfo? sokoFinalAmulet = null;
                SokobanRewardInfo? sokoFinalRing = null;
                SokobanRewardInfo? sokoFinalWand = null;
                List<SokobanRewardInfo>? sokoFloorRewards = null;

                dynamic? prevDownSquare = null;
                int prevMapCount = 0;

                for (int floor = 1; floor <= 40; floor++)
                {
                    // Check max remaining stop floor among undecided rules
                    int maxRemainingStop = 0;
                    bool needArtifacts = false;
                    bool needArtifactCount = false;
                    bool needCarriedCheck = false;
                    bool needMinetown = false;
                    bool needElf = false;
                    bool needSokoban = false;
                    bool needShops = false;

                    for (int r = 0; r < ruleCount; r++)
                    {
                        if (states[r] != RuleEvalState.Undecided) continue;
                        if (stopFloors[r] > maxRemainingStop) maxRemainingStop = stopFloors[r];
                        var rule = plan.Rules[r];
                        switch (rule.Type)
                        {
                            case "artifact":
                                needArtifacts = true;
                                if (!rule.AllowCarried) needCarriedCheck = true;
                                break;
                            case "artifact_count":
                                needArtifactCount = true;
                                break;
                            case "minetown":
                                if (floor == branches.Mines) needMinetown = true;
                                break;
                            case "elf":
                                if (floor == branches.Kingdom) needElf = true;
                                break;
                            case "sokoban":
                                if (floor == branches.Sokoban) needSokoban = true;
                                break;
                            case "shop":
                                needShops = true;
                                break;
                        }
                    }

                    if (floor > maxRemainingStop)
                    {
                        break;
                    }

                    // Generate floor `floor` (and any branch attached to `floor`)
                    prevDownSquare = GenerateSingleFloor(ctx, maker, generator, site, dungeonTerm, floor, 40, prevDownSquare, nextNum);

                    // Inspect ONLY the newly created maps from this floor iteration!
                    var worldMaps = (IList)adventure.World.Maps;
                    int currentMapCount = worldMaps.Count;

                    for (int mIdx = prevMapCount; mIdx < currentMapCount; mIdx++)
                    {
                        dynamic dm = worldMaps[mIdx]!;
                        string mapRawName = (string)(dm.Name ?? "");
                        var loc = SeedAnalyzer.ResolveMapLocation(dm, trans);

                        int dungeonLevel = loc.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase)
                            ? loc.LevelIndex
                            : loc.Site.Equals("Underdeep", StringComparison.OrdinalIgnoreCase)
                                ? branches.Mines + 4
                                : floor;

                        // 1. Lazy Artifact Scan
                        if (needArtifacts || needArtifactCount)
                        {
                            // Determine if ANY undecided artifact rule can match this map (site + level)
                            bool mapRelevantForAnyArtifactRule = needArtifactCount;
                            if (!mapRelevantForAnyArtifactRule)
                            {
                                for (int r = 0; r < ruleCount; r++)
                                {
                                    if (states[r] != RuleEvalState.Undecided) continue;
                                    var rule = plan.Rules[r];
                                    if (rule.Type != "artifact") continue;
                                    if (DoesArtifactMapMatch(rule, loc, dungeonLevel))
                                    {
                                        mapRelevantForAnyArtifactRule = true;
                                        break;
                                    }
                                }
                            }

                            if (mapRelevantForAnyArtifactRule)
                            {
                                seenArtifacts ??= new HashSet<object>();
                                HashSet<object>? carriedAssets = null;
                                List<object>? charPossessed = null;

                                foreach (var ch in (IEnumerable)dm.GetCharacters())
                                {
                                    dynamic dc = ch;
                                    if (dc.Inventory != null)
                                    {
                                        foreach (var a in (IEnumerable)dc.Inventory.GetPossessedAssets())
                                        {
                                            if (needCarriedCheck)
                                            {
                                                carriedAssets ??= new HashSet<object>();
                                                carriedAssets.Add(a);
                                            }
                                            charPossessed ??= new List<object>();
                                            charPossessed.Add(a);
                                        }
                                    }
                                }

                                void HandleAsset(object assetObj)
                                {
                                    if (!seenArtifacts.Add(assetObj)) return;
                                    dynamic da = assetObj;
                                    dynamic item = da.Item;
                                    if (item == null || item.Grade == null || (string)item.Grade.Name != "artifact") return;

                                    totalArtifacts++;
                                    bool isCarried = carriedAssets != null && carriedAssets.Contains(assetObj);
                                    string itemEn = (string)item.Name;
                                    string? itemCn = null;

                                    for (int r = 0; r < ruleCount; r++)
                                    {
                                        if (states[r] != RuleEvalState.Undecided) continue;
                                        var rule = plan.Rules[r];
                                        if (rule.Type == "artifact_count")
                                        {
                                            if (totalArtifacts >= rule.MinArtifacts!.Value)
                                            {
                                                states[r] = RuleEvalState.Matched;
                                            }
                                        }
                                        else if (rule.Type == "artifact")
                                        {
                                            if (!rule.AllowCarried && isCarried) continue;
                                            if (!DoesArtifactMapMatch(rule, loc, dungeonLevel)) continue;
                                            if (!string.IsNullOrEmpty(rule.ReqNameRaw))
                                            {
                                                itemCn ??= trans.TranslateItem(itemEn);
                                                bool nameMatches =
                                                    itemEn.Equals(rule.ReqNameEn, StringComparison.OrdinalIgnoreCase) ||
                                                    itemCn.Equals(rule.ReqNameRaw, StringComparison.OrdinalIgnoreCase) ||
                                                    itemEn.Contains(rule.ReqNameEn, StringComparison.OrdinalIgnoreCase) ||
                                                    itemCn.Contains(rule.ReqNameRaw, StringComparison.OrdinalIgnoreCase);
                                                if (!nameMatches) continue;
                                            }
                                            states[r] = RuleEvalState.Matched;
                                        }
                                    }
                                }

                                foreach (var asset in (IEnumerable)dm.WalkAssets())
                                {
                                    HandleAsset(asset);
                                }
                                if (charPossessed != null)
                                {
                                    foreach (var asset in charPossessed)
                                    {
                                        HandleAsset(asset);
                                    }
                                }
                            }
                        }

                        // 2. Lazy Minetown Scan
                        if (needMinetown && mapRawName.Contains("Minetown"))
                        {
                            int watchmen = 0, orcs = 0;
                            foreach (var c in (IEnumerable)dm.GetCharacters())
                            {
                                dynamic dc = c;
                                string ename = (string)(dc.Entity?.Name ?? "");
                                string cname = (string)(dc.Name ?? "");
                                string combined = $"{cname} {ename}".ToLowerInvariant();
                                if (combined.Contains("watch")) watchmen++;
                                if (combined.Contains("orc")) orcs++;
                            }

                            bool isOrcSiege = watchmen == 0 && orcs > 0;
                            for (int r = 0; r < ruleCount; r++)
                            {
                                if (states[r] != RuleEvalState.Undecided || plan.Rules[r].Type != "minetown") continue;
                                var rule = plan.Rules[r];
                                bool ok = rule.WantMinetownAny || (rule.WantMinetownOrc ? isOrcSiege : !isOrcSiege);
                                states[r] = ok ? RuleEvalState.Matched : RuleEvalState.Failed;
                            }
                        }

                        // 3. Lazy Elf Kingdom Scan
                        if (needElf)
                        {
                            string mapClean = mapRawName.Trim('_');
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
                                    if (ename.Equals("high elf", StringComparison.OrdinalIgnoreCase))
                                    {
                                        elfTotalHighElves++;
                                        if (isElfTown) elfTownHighElves++;
                                        if (isElfPalace) elfPalaceHighElves++;
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
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 4. Lazy Sokoban Scan
                        if (needSokoban && mapRawName.Contains("Sokoban"))
                        {
                            sokobanExists = true;
                            var fm = SokobanFloorRegex.Match(mapRawName);
                            int sokoFloor = fm.Success ? int.Parse(fm.Groups[1].Value) : 0;

                            // Only scan squares if at least one undecided sokoban rule checks prize
                            bool anyPrizeCheck = false;
                            for (int r = 0; r < ruleCount; r++)
                            {
                                if (states[r] == RuleEvalState.Undecided && plan.Rules[r].Type == "sokoban" &&
                                    !string.IsNullOrEmpty(plan.Rules[r].ReqSokobanPrizeRaw))
                                {
                                    anyPrizeCheck = true;
                                    break;
                                }
                            }

                            if (anyPrizeCheck && sokoFloor >= 1 && sokoFloor <= 4)
                            {
                                foreach (var sq in (IEnumerable)dm.GetSquares())
                                {
                                    dynamic dsq = sq;
                                    foreach (var a in (IEnumerable)dsq.GetAssets())
                                    {
                                        dynamic da = a;
                                        dynamic item = da.Item;
                                        if (item == null) continue;

                                        string iType = item.Type?.ToString() ?? "";
                                        if (sokoFloor == 4)
                                        {
                                            if (iType != "Bag" && iType != "Cloak" && iType != "Amulet" && iType != "Ring" && iType != "Wand")
                                                continue;
                                        }
                                        else if (iType != "Ring" && iType != "Wand")
                                        {
                                            continue;
                                        }

                                        string iName = (string)(item.Name ?? "");
                                        var reward = new SokobanRewardInfo
                                        {
                                            EnglishName = iName,
                                            ChineseName = trans.TranslateItem(iName),
                                            ItemType = iType,
                                            Floor = sokoFloor
                                        };

                                        if (sokoFloor == 4)
                                        {
                                            if (iType == "Bag" && sokoFinalBag == null) sokoFinalBag = reward;
                                            else if (iType == "Cloak" && sokoFinalCloak == null) sokoFinalCloak = reward;
                                            else if (iType == "Amulet" && sokoFinalAmulet == null) sokoFinalAmulet = reward;
                                            else if (iType == "Ring" && sokoFinalRing == null) sokoFinalRing = reward;
                                            else if (iType == "Wand" && sokoFinalWand == null) sokoFinalWand = reward;
                                        }
                                        else
                                        {
                                            sokoFloorRewards ??= new List<SokobanRewardInfo>();
                                            sokoFloorRewards.Add(reward);
                                        }
                                    }
                                }
                            }
                        }

                        // 5. Lazy Shop Scan (NO GetCharacters / NO GetSquares!)
                        if (needShops)
                        {
                            bool mapRelevantForAnyShopRule = false;
                            for (int r = 0; r < ruleCount; r++)
                            {
                                if (states[r] != RuleEvalState.Undecided) continue;
                                var rule = plan.Rules[r];
                                if (rule.Type != "shop") continue;
                                if (!rule.HasShopFilter || DoesShopMapMatch(rule, loc, dungeonLevel))
                                {
                                    mapRelevantForAnyShopRule = true;
                                    break;
                                }
                            }

                            if (mapRelevantForAnyShopRule)
                            {
                                void ScanTrigger(dynamic trig)
                                {
                                    foreach (var sched in (IEnumerable)trig.Schedules)
                                    {
                                        dynamic ds = sched;
                                        dynamic routine = ds.Routine;
                                        if (routine?.Trick == null) continue;
                                        string tname = (string)routine.Trick.Name;
                                        if (!tname.StartsWith("visited ")) continue;

                                        string shopEn = tname.Substring("visited ".Length);
                                        totalShops++;
                                        string? shopCn = null;

                                        for (int r = 0; r < ruleCount; r++)
                                        {
                                            if (states[r] != RuleEvalState.Undecided) continue;
                                            var rule = plan.Rules[r];
                                            if (rule.Type != "shop") continue;

                                            if (rule.HasShopFilter)
                                            {
                                                if (!DoesShopMapMatch(rule, loc, dungeonLevel)) continue;
                                                if (!string.IsNullOrEmpty(rule.ReqNameRaw))
                                                {
                                                    shopCn ??= trans.TranslateShop(shopEn);
                                                    bool matchName =
                                                        shopEn.Contains(rule.ReqNameEn, StringComparison.OrdinalIgnoreCase) ||
                                                        shopCn.Contains(rule.ReqNameRaw, StringComparison.OrdinalIgnoreCase) ||
                                                        shopEn.Contains(rule.ReqNameRaw, StringComparison.OrdinalIgnoreCase);
                                                    if (!matchName) continue;
                                                }
                                                states[r] = RuleEvalState.Matched;
                                            }
                                            else if (rule.MinShops.HasValue)
                                            {
                                                if (totalShops >= rule.MinShops.Value)
                                                {
                                                    states[r] = RuleEvalState.Matched;
                                                }
                                            }
                                        }
                                    }
                                }

                                if (dm.Trigger != null) ScanTrigger(dm.Trigger);
                                foreach (var zone in (IEnumerable)dm.Zones)
                                {
                                    dynamic dz = zone;
                                    if (dz.Trigger != null) ScanTrigger(dz.Trigger);
                                }
                            }
                        }
                    }

                    prevMapCount = currentMapCount;

                    // Finalize Elf rules after finishing Kingdom floor
                    if (needElf && floor == branches.Kingdom)
                    {
                        bool hasElf = elfSiteDetected || elfKingFound || elfTotalHighElves > 0;
                        bool isMassacre = (elfTownHighElves == 0 && elfPalaceHighElves > 0) || elfKingCarriesArtifact;
                        for (int r = 0; r < ruleCount; r++)
                        {
                            if (states[r] != RuleEvalState.Undecided || plan.Rules[r].Type != "elf") continue;
                            if (!hasElf)
                            {
                                states[r] = RuleEvalState.Failed;
                                continue;
                            }
                            var rule = plan.Rules[r];
                            bool ok = rule.WantElfAny || (rule.WantElfMassacre ? isMassacre : !isMassacre);
                            states[r] = ok ? RuleEvalState.Matched : RuleEvalState.Failed;
                        }
                    }

                    // Finalize Sokoban rules after finishing Sokoban floor
                    if (needSokoban && floor == branches.Sokoban)
                    {
                        for (int r = 0; r < ruleCount; r++)
                        {
                            if (states[r] != RuleEvalState.Undecided || plan.Rules[r].Type != "sokoban") continue;
                            if (!sokobanExists)
                            {
                                states[r] = RuleEvalState.Failed;
                                continue;
                            }
                            var rule = plan.Rules[r];
                            if (string.IsNullOrEmpty(rule.ReqSokobanPrizeRaw))
                            {
                                states[r] = RuleEvalState.Matched;
                                continue;
                            }

                            bool MatchesPrize(SokobanRewardInfo? info)
                            {
                                if (info == null) return false;
                                return info.EnglishName.Contains(rule.ReqSokobanPrizeEn, StringComparison.OrdinalIgnoreCase) ||
                                       info.ChineseName.Contains(rule.ReqSokobanPrizeRaw, StringComparison.OrdinalIgnoreCase) ||
                                       info.EnglishName.Contains(rule.ReqSokobanPrizeRaw, StringComparison.OrdinalIgnoreCase);
                            }

                            bool prizeMatched = MatchesPrize(sokoFinalBag) || MatchesPrize(sokoFinalCloak) ||
                                                MatchesPrize(sokoFinalAmulet) || MatchesPrize(sokoFinalRing) ||
                                                MatchesPrize(sokoFinalWand) ||
                                                (sokoFloorRewards != null && sokoFloorRewards.Any(MatchesPrize));

                            states[r] = prizeMatched ? RuleEvalState.Matched : RuleEvalState.Failed;
                        }
                    }

                    // Any undecided rule whose StopFloor has been reached is now definitively Failed!
                    for (int r = 0; r < ruleCount; r++)
                    {
                        if (states[r] == RuleEvalState.Undecided && stopFloors[r] <= floor)
                        {
                            states[r] = RuleEvalState.Failed;
                        }
                    }

                    overall = EvaluateOverall(plan, states);
                    if (overall != RuleEvalState.Undecided)
                    {
                        break;
                    }
                }
            }

            if (overall != RuleEvalState.Matched)
            {
                return null;
            }

            // Stage B: Full Hydration + Ground-Truth Double-Check (only runs on matching hits!)
            var fullReport = SeedAnalyzer.Analyze(seedInput, ctx);
            return SeedFilter.Matches(fullReport, plan.Criteria) ? fullReport : null;
        }

        private static bool DoesArtifactMapMatch(CompiledRule rule, MapLocation loc, int dungeonLevel)
        {
            if (!rule.IsAnySite)
            {
                bool isArtDungeon = loc.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                    loc.SiteChinese.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                    loc.SiteChinese.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                bool siteMatches = rule.IsDungeonSiteOnly ? isArtDungeon :
                    (loc.Site.Contains(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase) ||
                     loc.SiteChinese.Contains(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrEmpty(loc.SubArea) && loc.SubArea.Contains(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase)));

                if (!siteMatches) return false;
            }

            if (rule.MaxLevel.HasValue)
            {
                bool isDungeonReq = rule.IsAnySite || rule.IsDungeonSiteOnly;
                bool levelMatches = !isDungeonReq
                    ? (loc.LevelIndex <= rule.MaxLevel.Value || dungeonLevel <= rule.MaxLevel.Value)
                    : (dungeonLevel <= rule.MaxLevel.Value);
                if (!levelMatches) return false;
            }

            return true;
        }

        private static bool DoesShopMapMatch(CompiledRule rule, MapLocation loc, int dungeonLevel)
        {
            if (!rule.IsAnySite)
            {
                bool isShopDungeon = loc.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                     loc.SiteChinese.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                     loc.SiteChinese.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                bool siteMatches = rule.IsDungeonSiteOnly ? isShopDungeon :
                    (loc.Site.Equals(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase) ||
                     loc.SiteChinese.Equals(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase) ||
                     loc.Site.Contains(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase) ||
                     loc.SiteChinese.Contains(rule.ReqSiteRaw, StringComparison.OrdinalIgnoreCase));

                if (!siteMatches) return false;
            }

            if (rule.MaxLevel.HasValue)
            {
                int levelToCheck = dungeonLevel > 0 ? dungeonLevel : loc.LevelIndex;
                if (levelToCheck > rule.MaxLevel.Value) return false;
            }

            return true;
        }

        private static RuleEvalState EvaluateOverall(CompiledFilterPlan plan, RuleEvalState[] states)
        {
            if (plan.IsAnyMode)
            {
                bool allFailed = true;
                for (int r = 0; r < states.Length; r++)
                {
                    if (states[r] == RuleEvalState.Matched) return RuleEvalState.Matched;
                    if (states[r] == RuleEvalState.Undecided) allFailed = false;
                }
                return allFailed ? RuleEvalState.Failed : RuleEvalState.Undecided;
            }

            bool allMustMatched = true;
            bool anyOptMatched = false;
            bool allOptFailed = true;

            for (int r = 0; r < states.Length; r++)
            {
                if (!plan.Rules[r].IsOptional)
                {
                    if (states[r] == RuleEvalState.Failed) return RuleEvalState.Failed;
                    if (states[r] == RuleEvalState.Undecided) allMustMatched = false;
                }
                else
                {
                    if (states[r] == RuleEvalState.Matched) anyOptMatched = true;
                    if (states[r] != RuleEvalState.Failed) allOptFailed = false;
                }
            }

            if (plan.HasOptionalRules && !anyOptMatched && allOptFailed)
            {
                return RuleEvalState.Failed;
            }

            if (allMustMatched && (!plan.HasOptionalRules || anyOptMatched))
            {
                return RuleEvalState.Matched;
            }

            return RuleEvalState.Undecided;
        }
    }
}
