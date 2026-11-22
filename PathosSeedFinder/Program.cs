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
