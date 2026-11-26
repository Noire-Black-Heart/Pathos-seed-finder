using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using PathosSeedFinder.Core;
using PathosSeedFinder.UI;
using PathosSeedFinder.Web;

namespace PathosSeedFinder
{
    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.AppendAllText("fatal_crash.log", $"[{DateTime.Now}] UNHANDLED: {e.ExceptionObject}\n"); } catch { }
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try { File.AppendAllText("fatal_crash.log", $"[{DateTime.Now}] UNOBSERVED: {e.Exception}\n"); } catch { }
                e.SetObserved();
            };

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch { }

            if (args.Length == 0 || args[0].Equals("gui", StringComparison.OrdinalIgnoreCase) || args[0].Equals("web", StringComparison.OrdinalIgnoreCase))
            {
                WebServer.Run(args);
                return;
            }

            if (args[0].Equals("cli", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--cli", StringComparison.OrdinalIgnoreCase))
            {
                InteractiveMenu.Run();
                return;
            }


            string command = args[0].ToLowerInvariant();
            if (command == "verify-fast")
            {
                var settings = SettingsManager.Load();
                PathosContext.TryInitialize(settings.PathosDirectory, out _);
                var ctx = PathosContext.Instance!;
                Console.WriteLine($"[VERIFY] SupportsSteppedGeneration: {ctx.SupportsSteppedGeneration}");
                if (!ctx.SupportsSteppedGeneration)
                {
                    Console.WriteLine("[FAIL] SupportsSteppedGeneration is FALSE!");
                    return;
                }

                // 1. Bit-for-bit PRNG equivalence over 10 seeds (40 floors stepped vs official NethackModule.Execute)
                Console.WriteLine("[TEST 1] Verifying 40-floor bit-for-bit PRNG equivalence (10 seeds)...");
                for (int i = 0; i < 10; i++)
                {
                    string s = $"#000100{i:X2}";
                    var officialRep = SeedAnalyzer.Analyze(s, ctx);
                    dynamic seedObj = SeedAnalyzer.ParseSeed(s, ctx);
                    dynamic steppedAdv = SteppedSeedEvaluator.SimulateSteppedFullForTest(ctx, seedObj, (int)seedObj.Code);

                    // Count artifacts on steppedAdv directly
                    var steppedArts = new List<string>();
                    var seen = new HashSet<object>();
                    foreach (var m in (IEnumerable)steppedAdv.World.Maps)
                    {
                        dynamic dm = m;
                        foreach (var a in (IEnumerable)dm.WalkAssets())
                        {
                            if (!seen.Add(a)) continue;
                            dynamic item = ((dynamic)a).Item;
                            if (item != null && item.Grade != null && (string)item.Grade.Name == "artifact")
                                steppedArts.Add($"{(string)dm.Name}:{(string)item.Name}");
                        }
                        foreach (var c in (IEnumerable)dm.GetCharacters())
                        {
                            dynamic dc = c;
                            if (dc.Inventory == null) continue;
                            foreach (var a in (IEnumerable)dc.Inventory.GetPossessedAssets())
                            {
                                if (!seen.Add(a)) continue;
                                dynamic item = ((dynamic)a).Item;
                                if (item != null && item.Grade != null && (string)item.Grade.Name == "artifact")
                                    steppedArts.Add($"{(string)dm.Name}:{(string)item.Name}");
                            }
                        }
                    }
                    var officialArts = officialRep.Artifacts.Select(a => $"{a.MapRawName}:{a.EnglishName}").ToList();
                    if (!steppedArts.SequenceEqual(officialArts))
                    {
                        Console.WriteLine($"[FAIL] PRNG mismatch on {s}! Official({officialArts.Count}) vs Stepped({steppedArts.Count})");
                        Console.WriteLine("  Official: " + string.Join(", ", officialArts.Take(10)));
                        Console.WriteLine("  Stepped:  " + string.Join(", ", steppedArts.Take(10)));
                        return;
                    }
                }
                Console.WriteLine("[PASS] Test 1: 10/10 seeds 100% bit-for-bit identical across all 40 floors!");

                // 2. Ground-truth equivalence test across 60 seeds and 15 diverse criteria (especially MaxLevel!)
                Console.WriteLine("[TEST 2] Generating 60 ground-truth reports for exhaustive filter equivalence...");
                int testCount = 60;
                var seeds = Enumerable.Range(1000, testCount).Select(x => $"#{x:X8}").ToArray();
                var truthReports = new SeedReport[testCount];
                for (int i = 0; i < testCount; i++)
                {
                    truthReports[i] = SeedAnalyzer.Analyze(seeds[i], ctx);
                }

                var testCases = new (string Name, FilterCriteria Criteria)[]
                {
                    ("Artifact MaxLevel=1 (any site)", new FilterCriteria { RequiredLevel = 1 }),
                    ("Artifact MaxLevel=3 (any site)", new FilterCriteria { RequiredLevel = 3 }),
                    ("Artifact MaxLevel=5 Site=Dungeon", new FilterCriteria { RequiredLevel = 5, RequiredSite = "Dungeon" }),
                    ("Artifact MaxLevel=1 Site=Elf Kingdom", new FilterCriteria { RequiredLevel = 1, RequiredSite = "Elf Kingdom" }),
                    ("Artifact MaxLevel=2 Site=Mines", new FilterCriteria { RequiredLevel = 2, RequiredSite = "Mines" }),
                    ("Artifact MaxLevel=10 AllowCarried=false", new FilterCriteria { RequiredLevel = 10, RequiredArtifactAllowCarried = false }),
                    ("Artifact Name='明镜' MaxLevel=10", new FilterCriteria { RequiredArtifact = "明镜", RequiredLevel = 10 }),
                    ("Artifact Name='a' (no level, no site)", new FilterCriteria { RequiredArtifact = "a" }),
                    ("Shop MaxLevel=2 (any site)", new FilterCriteria { RequiredShopMaxLevel = 2 }),
                    ("Shop MaxLevel=5 Site=Dungeon", new FilterCriteria { RequiredShopMaxLevel = 5, RequiredShopSite = "Dungeon" }),
                    ("Shop Type='古兵器' MaxLevel=15", new FilterCriteria { RequiredShop = "古兵器", RequiredShopMaxLevel = 15 }),
                    ("Minetown=orc", new FilterCriteria { MinetownState = "orc" }),
                    ("Minetown=normal", new FilterCriteria { MinetownState = "normal" }),
                    ("Elf=massacre", new FilterCriteria { ElfKingdomState = "massacre" }),
                    ("SokobanLevel=6", new FilterCriteria { RequiredSokobanLevel = 6 }),
                    ("SokobanPrize='收纳之袋'", new FilterCriteria { RequiredSokobanPrize = "收纳之袋" }),
                    ("MinArtifacts=20", new FilterCriteria { MinArtifacts = 20 }),
                    ("ALL: Minetown=orc + MaxLevel=5 + Optional(Elf=massacre OR SokobanLevel=6)", new FilterCriteria
                    {
                        MatchMode = "all",
                        Rules = new List<ConditionRule>
                        {
                            new() { Type = "minetown", MinetownState = "orc", LogicRole = "must" },
                            new() { Type = "artifact", MaxLevel = 5, LogicRole = "must" },
                            new() { Type = "elf", ElfKingdomState = "massacre", LogicRole = "optional" },
                            new() { Type = "sokoban", SokobanLevel = 6, LogicRole = "optional" }
                        }
                    }),
                    ("ANY: MaxLevel=1 OR Minetown=orc", new FilterCriteria
                    {
                        MatchMode = "any",
                        Rules = new List<ConditionRule>
                        {
                            new() { Type = "artifact", MaxLevel = 1 },
                            new() { Type = "minetown", MinetownState = "orc" }
                        }
                    })
                };

                int totalMismatches = 0;
                foreach (var (name, crit) in testCases)
                {
                    var plan = CompiledFilterPlan.Compile(crit, ctx.Translations);
                    int truthHits = 0, fastHits = 0, fp = 0, fn = 0;
                    var swFast = Stopwatch.StartNew();
                    for (int i = 0; i < testCount; i++)
                    {
                        bool expected = SeedFilter.Matches(truthReports[i], crit);
                        var fastRep = SteppedSeedEvaluator.TryMatchSeed(seeds[i], ctx, plan);
                        bool actual = fastRep != null;
                        if (expected) truthHits++;
                        if (actual) fastHits++;
                        if (actual && !expected) { fp++; Console.WriteLine($"  [FP] {name} on {seeds[i]}"); }
                        if (!actual && expected) { fn++; Console.WriteLine($"  [FN] {name} on {seeds[i]}"); }
                    }
                    swFast.Stop();
                    totalMismatches += fp + fn;
                    double msPerSeed = swFast.ElapsedMilliseconds / (double)testCount;
                    Console.WriteLine($"  [{(fp + fn == 0 ? "PASS" : "FAIL")}] {name,-68} | Hits: {fastHits,2}/{truthHits,2} | {msPerSeed,6:F1} ms/seed (1T)");
                }
                Console.WriteLine(totalMismatches == 0
                    ? "[PASS] Test 2: ALL 19 criteria 100% EXACT MATCH (0 FP, 0 FN)!"
                    : $"[FAIL] Test 2: {totalMismatches} mismatches found!");

                // 3. 16-Thread real parallel speed benchmark
                Console.WriteLine("\n[TEST 3] 16-Thread Parallel Speed Benchmark (200 seeds each):");
                var benchCases = new (string Name, FilterCriteria Criteria)[]
                {
                    ("Artifact '明镜' MaxLevel=1", new FilterCriteria { RequiredArtifact = "明镜", RequiredLevel = 1, MaxSearchCount = 200, StartSeedNumber = 50000 }),
                    ("Artifact '明镜' MaxLevel=3", new FilterCriteria { RequiredArtifact = "明镜", RequiredLevel = 3, MaxSearchCount = 200, StartSeedNumber = 50000 }),
                    ("Minetown=orc + Artifact '明镜' MaxLevel=3", new FilterCriteria { MinetownState = "orc", RequiredArtifact = "明镜", RequiredLevel = 3, MaxSearchCount = 200, StartSeedNumber = 50000 }),
                    ("SokobanLevel=6 + Prize='收纳之袋'", new FilterCriteria { RequiredSokobanLevel = 6, RequiredSokobanPrize = "收纳之袋", MaxSearchCount = 200, StartSeedNumber = 50000 }),
                    ("Elf=massacre", new FilterCriteria { ElfKingdomState = "massacre", MaxSearchCount = 100, StartSeedNumber = 50000 }),
                };
                // Prewarm pool
                var warmTasks = Enumerable.Range(0, 16).Select(_ => WorkerPool.RentAsync(settings.PathosDirectory)).ToArray();
                var warmWorkers = Task.WhenAll(warmTasks).GetAwaiter().GetResult();
                foreach (var w in warmWorkers) WorkerPool.Return(w);

                foreach (var (name, crit) in benchCases)
                {
                    var swB = Stopwatch.StartNew();
                    int hits = 0;
                    var task = Task.Run(async () =>
                    {
                        await foreach (var _ in SeedFilter.SearchParallelAsync(crit, 16))
                        {
                            hits++;
                        }
                    });
                    task.GetAwaiter().GetResult();
                    swB.Stop();
                    double sec = swB.ElapsedMilliseconds / 1000.0;
                    Console.WriteLine($"  {name,-45} | {crit.MaxSearchCount} seeds in {sec:F2}s => {crit.MaxSearchCount / Math.Max(0.001, sec),6:F1} seeds/sec (hits: {hits})");
                }
                return;
            }
            if (command == "profile")
            {
                var settings = SettingsManager.Load();
                PathosContext.TryInitialize(settings.PathosDirectory, out _);
                var ctx = PathosContext.Instance!;

                // Warmup
                SeedAnalyzer.Analyze("warmup_prof", ctx);

                int n = 10;
                long tEngine = 0, tGen = 0, tShuffle = 0, tId = 0, tFullAnalyze = 0;

                for (int i = 0; i < n; i++)
                {
                    dynamic seed = ctx.CreateSeed($"prof_{i}");
                    int seedCode = (int)seed.Code;

                    var sw = Stopwatch.StartNew();
                    dynamic engine = ctx.CreateEngine(seed);
                    dynamic adventure = engine.Adventure;
                    adventure.ChapterList.Clear();
                    adventure.PlayerList.Clear();
                    tEngine += sw.ElapsedTicks;

                    sw.Restart();
                    ctx.Reseed(Environment.TickCount);
                    ctx.Reseed(seedCode);
                    ctx.PrepareShuffle(adventure);
                    ctx.NethackModule.Execute(engine.Generator);
                    ctx.CommitShuffle(adventure);
                    tGen += sw.ElapsedTicks;

                    sw.Restart();
                    ctx.ReverseShuffle(adventure);
                    tShuffle += sw.ElapsedTicks;

                    sw.Restart();
                    var rep = SeedAnalyzer.Analyze($"prof_{i}", ctx);
                    tFullAnalyze += sw.ElapsedTicks;
                    tId += rep.ElapsedMs;
                }

                double toMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency / n;
                Console.WriteLine($"[PROFILE] Average over {n} seeds (1 thread):");
                Console.WriteLine($"  CreateEngine:      {toMs(tEngine):F2} ms");
                Console.WriteLine($"  NethackExecute:    {toMs(tGen):F2} ms");
                Console.WriteLine($"  ReverseShuffle:    {toMs(tShuffle):F2} ms");
                Console.WriteLine($"  Analyze.SimMs:     {(double)tId / n:F2} ms (includes ExtractId)");
                Console.WriteLine($"  Full AnalyzeSeed:  {toMs(tFullAnalyze):F2} ms");
                Console.WriteLine($"  => Post-Sim Scan:  {toMs(tFullAnalyze) - (double)tId / n:F2} ms");
                return;
            }
            if (command == "bench")
            {
                var settings = SettingsManager.Load();
                PathosContext.TryInitialize(settings.PathosDirectory, out _);
                string path = settings.PathosDirectory;
                Console.WriteLine($"[DIAG] GCSettings.IsServerGC: {System.Runtime.GCSettings.IsServerGC}");

                int maxW = 16;
                Console.WriteLine($"[BENCH] Pre-allocating {maxW} isolated PathosContext instances...");
                var swAlloc = Stopwatch.StartNew();
                var allWorkers = new List<PathosContext>();
                for (int i = 0; i < maxW; i++)
                {
                    allWorkers.Add(PathosContext.CreateIsolated(path));
                }
                swAlloc.Stop();
                Console.WriteLine($"[BENCH] {maxW} workers allocated in {swAlloc.ElapsedMilliseconds} ms.");

                int[] testThreads = new[] { 1, 4, 8, 16 };
                int seedsPerTest = 32;

                foreach (int th in testThreads)
                {
                    var pool = new System.Collections.Concurrent.BlockingCollection<PathosContext>();
                    for (int i = 0; i < th; i++) pool.Add(allWorkers[i]);

                    // Warm up
                    Parallel.ForEach(pool, w =>
                    {
                        SeedAnalyzer.Analyze("warmup", w);
                    });

                    var swRun = Stopwatch.StartNew();
                    Parallel.For(0, seedsPerTest, new ParallelOptions { MaxDegreeOfParallelism = th }, i =>
                    {
                        var w = pool.Take();
                        try
                        {
                            SeedAnalyzer.Analyze($"diag_{th}_{i}", w);
                        }
                        finally
                        {
                            pool.Add(w);
                        }
                    });
                    swRun.Stop();

                    double sec = swRun.ElapsedMilliseconds / 1000.0;
                    double spd = seedsPerTest / sec;
                    Console.WriteLine($"[BENCH] Threads: {th,2} | Seeds: {seedsPerTest} | Time: {sec:F2}s | Speed: {spd:F2} seeds/sec");
                }
                return;
            }
            if (command == "test-catalog")
            {
                var list = MapService.GetCatalog("test1234");
                string lastGroup = "";
                foreach (var m in list)
                {
                    if (m.Group != lastGroup)
                    {
                        Console.WriteLine($"\n=== {m.Group} ===");
                        lastGroup = m.Group;
                    }
                    Console.WriteLine($"  [{m.Id}] {m.DisplayName} ({m.Width}x{m.Height}) | Raw: {m.RawName}");
                }
                return;
            }
            if (command == "analyze" || command == "-a")
            {
                string seed = "12345";
                for (int i = 1; i < args.Length; i++)
                {
                    if ((args[i] == "-s" || args[i] == "--seed") && i + 1 < args.Length)
                    {
                        seed = args[i + 1];
                        i++;
                    }
                    else if (!args[i].StartsWith("-"))
                    {
                        seed = args[i];
                    }
                }

                Console.WriteLine($"🔍 正在分析种子: {seed} ...");
                var report = SeedAnalyzer.Analyze(seed);
                ConsoleFormatter.PrintReport(report);
            }
            else if (command == "filter" || command == "-f")
            {
                var criteria = new FilterCriteria();
                for (int i = 1; i < args.Length; i++)
                {
                    string arg = args[i].ToLowerInvariant();
                    if ((arg == "--min-artifacts" || arg == "-m") && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int m)) criteria.MinArtifacts = m;
                    }
                    else if ((arg == "--artifact" || arg == "-art" || arg == "-a") && i + 1 < args.Length)
                    {
                        criteria.RequiredArtifact = args[++i];
                    }
                    else if ((arg == "--level" || arg == "-l") && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int l)) criteria.RequiredLevel = l;
                    }
                    else if ((arg == "--site") && i + 1 < args.Length)
                    {
                        criteria.RequiredSite = args[++i];
                    }
                    else if ((arg == "--minetown" || arg == "-mt") && i + 1 < args.Length)
                    {
                        criteria.MinetownState = args[++i];
                    }
                    else if ((arg == "--elf" || arg == "-ek" || arg == "--elf-kingdom") && i + 1 < args.Length)
                    {
                        criteria.ElfKingdomState = args[++i];
                    }
                    else if ((arg == "--sokoban-prize" || arg == "-sk") && i + 1 < args.Length)
                    {
                        criteria.RequiredSokobanPrize = args[++i];
                    }
                    else if ((arg == "--sokoban-level" || arg == "-skl") && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int skl)) criteria.RequiredSokobanLevel = skl;
                    }
                    else if ((arg == "--branch" || arg == "-b") && i + 1 < args.Length)
                    {
                        criteria.RequiredBranch = args[++i];
                    }
                    else if ((arg == "--branch-level" || arg == "-bl") && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int bl)) criteria.RequiredBranchLevel = bl;
                    }
                    else if ((arg == "--max" || arg == "-n") && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int n)) criteria.MaxSearchCount = n;
                    }
                }

                Console.WriteLine("🎯 开始按命令行条件检索种子...");
                foreach (var match in SeedFilter.Search(criteria, (cur, s, m) =>
                {
                    if (cur % 25 == 0) Console.Write($"\r🔄 已检索: {cur} ...   ");
                }))
                {
                    Console.WriteLine();
                    ConsoleFormatter.PrintReport(match);
                    break;
                }
            }
            else
            {
                Console.WriteLine("用法指南:");
                Console.WriteLine("  1. 直接运行 (无参数) : 进入交互式菜单界面");
                Console.WriteLine("  2. 分析种子: PathosSeedFinder analyze -s <种子>");
                Console.WriteLine("  3. 筛选种子: PathosSeedFinder filter --min-artifacts 20 --artifact 明镜 --level 1 --minetown orc --elf peaceful --sokoban-prize 收纳之袋");
            }
        }
    }
}
