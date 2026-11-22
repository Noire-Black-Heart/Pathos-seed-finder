using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PathosSeedFinder.Core;

namespace PathosSeedFinder.UI
{
    public static class InteractiveMenu
    {
        public static void Run()
        {
            while (true)
            {
                Console.Clear();
                ConsoleFormatter.PrintHeader("🌟 Pathos: Nethack Codex 随机种子分析与筛选神器 🌟");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [1] 🔍 分析指定种子 (查看商人、神器、矿镇状态等详细情报)");
                Console.WriteLine("  [2] 🎯 按条件筛选种子 (指定神器、层数、神器数量、矿镇状态)");
                Console.WriteLine("  [3] 🎲 极速抽取优质高爆种子 (自动寻找 >= 20 神器种子)");
                Console.WriteLine("  [4] 📖 神器与设施对照名鉴 (支持中文与英文对照搜索)");
                Console.WriteLine("  [5] ⚙️  系统与安装环境信息");
                Console.WriteLine("  [0] 🚪 退出程序");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("👉 请输入选项 [0-5]: ");

                string? choice = Console.ReadLine()?.Trim();
                switch (choice)
                {
                    case "1":
                        AnalyzeSingleSeedFlow();
                        break;
                    case "2":
                        FilterSeedsFlow();
                        break;
                    case "3":
                        QuickRollHighArtifactSeedFlow();
                        break;
                    case "4":
                        ShowGlossaryFlow();
                        break;
                    case "5":
                        ShowEnvironmentFlow();
                        break;
                    case "0":
                    case "exit":
                    case "q":
                        Console.WriteLine("\n感谢使用，祝御主在地牢探险顺利！🌟");
                        return;
                    default:
                        Console.WriteLine("无效选项，请重新输入。");
                        Thread.Sleep(800);
                        break;
                }
            }
        }

        private static void AnalyzeSingleSeedFlow()
        {
            Console.Clear();
            ConsoleFormatter.PrintHeader("🔍 分析指定种子");
            Console.WriteLine();
            Console.WriteLine("💡 提示: Pathos 游戏中官方支持且跨进程固定一致的种子格式是带有 '#' 的十六进制编码（如 #12345678）。");
            Console.WriteLine("         若直接输入 8 位十六进制（如 12345678），工具会自动添加 '#' 与游戏内完全对齐。");
            Console.WriteLine();
            Console.Write("👉 请输入种子 (例如 #12345678): ");
            string? input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("种子不能为空。按任意键返回...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\n⏳ 正在分析种子 '{input}'，请稍候...");
            try
            {
                var report = SeedAnalyzer.Analyze(input);
                ConsoleFormatter.PrintReport(report);

                Console.Write("💾 是否将此报告保存为 Markdown 文件？(Y/N, 默认 N): ");
                string? saveChoice = Console.ReadLine()?.Trim().ToUpperInvariant();
                if (saveChoice == "Y")
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                    Directory.CreateDirectory(dir);
                    string fileName = $"Seed_{report.SeedText.Replace(" ", "_")}_Report.md";
                    string filePath = Path.Combine(dir, fileName);
                    File.WriteAllText(filePath, ConsoleFormatter.GenerateMarkdownReport(report));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ 报告已成功导出至: {filePath}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 分析失败: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\n按任意键返回主菜单...");
            Console.ReadKey();
        }

        private static void FilterSeedsFlow()
        {
            Console.Clear();
            ConsoleFormatter.PrintHeader("🎯 自定义条件筛选种子");
            Console.WriteLine();
            Console.WriteLine("💡 提示: 每一项条件可直接按回车跳过 (表示不限)。");
            Console.WriteLine();

            var criteria = new FilterCriteria();

            // 1. Min artifacts
            Console.Write("1. 最少神器总数量 (如输入 20，直接回车不限): ");
            string? artCountStr = Console.ReadLine()?.Trim();
            if (int.TryParse(artCountStr, out int minArts) && minArts > 0)
            {
                criteria.MinArtifacts = minArts;
            }

            // 2. Specific artifact
            Console.Write("2. 指定神器名称 (支持中文或英文，如 '明镜' 或 'Mirrorbright'，回车不限): ");
            string? artName = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(artName))
            {
                criteria.RequiredArtifact = artName;
            }

            // 3. Level constraint
            Console.Write("3. 要求该神器的获取层数/主地牢折算深度 (如开局第 2 层输入 2，分支按入口折算，回车不限): ");
            string? levelStr = Console.ReadLine()?.Trim();
            if (int.TryParse(levelStr, out int lvl) && lvl >= 0)
            {
                criteria.RequiredLevel = lvl;
            }

            // 3.1 Optional site constraint
            Console.Write("   是否限定只在【主地牢】开局直接拾取？(Y/N，输入 Y 排除所有分支，默认 N 回车不限): ");
            string? siteChoice = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (siteChoice == "Y")
            {
                criteria.RequiredSite = "Dungeon";
            }

            // 4. Minetown state
            Console.WriteLine("4. 矿镇状态要求:");
            Console.WriteLine("   [1] 正常守卫巡逻 (Peaceful / Guarded)");
            Console.WriteLine("   [2] 兽人屠城/攻城状态 (Orc Massacre)");
            Console.WriteLine("   [回车] 不限状态");
            Console.Write("   请选择 [1/2/回车]: ");
            string? mtChoice = Console.ReadLine()?.Trim();
            if (mtChoice == "1") criteria.MinetownState = "normal";
            else if (mtChoice == "2") criteria.MinetownState = "orc";

            // 5. Elf Kingdom state
            Console.WriteLine("5. 精灵王国政局状态要求:");
            Console.WriteLine("   [1] 盛世繁荣/和平友好 (Prosperous & Peaceful)");
            Console.WriteLine("   [2] 屠城惨案/暴动敌对 (Elf Massacre & Rebellion)");
            Console.WriteLine("   [回车] 不限状态");
            Console.Write("   请选择 [1/2/回车]: ");
            string? ekChoice = Console.ReadLine()?.Trim();
            if (ekChoice == "1") criteria.ElfKingdomState = "peaceful";
            else if (ekChoice == "2") criteria.ElfKingdomState = "massacre";

            // 6. Sokoban prize
            Console.Write("6. 仓库番终极奖励要求 (如 '收纳之袋' 或 '反射护符'，回车不限): ");
            string? sokoPrize = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(sokoPrize))
            {
                criteria.RequiredSokobanPrize = sokoPrize;
            }

            // 7. Branch entrance
            Console.Write("7. 指定分支区域入口 (如 '卢底人堡垒'、'矿洞'、'迷宫' 等，回车不限): ");
            string? branchName = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                criteria.RequiredBranch = branchName;
            }

            // 8. Branch level
            Console.Write("8. 要求该分支入口所在的地牢层数 (如 8，回车不限): ");
            string? branchLvlStr = Console.ReadLine()?.Trim();
            if (int.TryParse(branchLvlStr, out int bLvl) && bLvl > 0)
            {
                criteria.RequiredBranchLevel = bLvl;
            }

            // 9. Max seeds to search
            Console.Write("9. 最大检索种子数量 (默认 300，回车使用默认值): ");
            string? maxSearchStr = Console.ReadLine()?.Trim();
            if (int.TryParse(maxSearchStr, out int maxCount) && maxCount > 0)
            {
                criteria.MaxSearchCount = maxCount;
            }
            else
            {
                criteria.MaxSearchCount = 300;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("🚀 开始按条件检索种子，按 Ctrl+C 可提前停止检索...");
            Console.ResetColor();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\n⚠️ 正在停止搜索...");
            };

            var matches = new List<SeedReport>();
            int testedCount = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                foreach (var match in SeedFilter.Search(criteria, (current, seed, m) =>
                {
                    testedCount = current;
                    if (current % 10 == 0 || m != null)
                    {
                        Console.Write($"\r🔄 已检索: {current}/{criteria.MaxSearchCount} 种子 (已找到匹配: {matches.Count + (m != null ? 1 : 0)}) ...    ");
                    }
                }, cts.Token))
                {
                    matches.Add(match);
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n🎉 [命中匹配!] 种子: '{match.SeedText}' (Code: {match.SeedCode})");
                    Console.WriteLine($"   * 神器总数: {match.TotalArtifacts} 件");
                    if (match.Minetown != null)
                        Console.WriteLine($"   * 矿镇状态: {match.Minetown.StatusDescription}");
                    Console.ResetColor();

                    Console.Write("   是否停止搜索并查看详情？(Y/N, 默认 Y): ");
                    string? stopChoice = Console.ReadLine()?.Trim().ToUpperInvariant();
                    if (stopChoice != "N")
                    {
                        ConsoleFormatter.PrintReport(match);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine($"\n🏁 检索结束! 共测试 {testedCount} 个种子，找到 {matches.Count} 个匹配，总耗时 {sw.ElapsedMilliseconds} ms (平均 {sw.ElapsedMilliseconds / Math.Max(1, testedCount)} ms/种子)。");

            Console.WriteLine("\n按任意键返回主菜单...");
            Console.ReadKey();
        }

        private static void QuickRollHighArtifactSeedFlow()
        {
            Console.Clear();
            ConsoleFormatter.PrintHeader("🎲 极速抽取优质高爆种子 (>= 20 神器)");
            Console.WriteLine();
            Console.WriteLine("⏳ 正在全力 roll 种中，请稍候...");

            var criteria = new FilterCriteria
            {
                MinArtifacts = 20,
                MaxSearchCount = 200,
                SeedPrefix = "#",
                StartSeedNumber = Random.Shared.Next(1, 0x7FFFFFFF)
            };

            SeedReport? found = null;
            foreach (var match in SeedFilter.Search(criteria, (c, s, m) =>
            {
                Console.Write($"\r🔄 正在探索第 {c} 个种子: {s} ...    ");
            }))
            {
                found = match;
                break;
            }

            Console.WriteLine();
            if (found != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✨ 成功找到超优质高神器种子: '{found.SeedText}'！");
                Console.ResetColor();
                ConsoleFormatter.PrintReport(found);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n未能在此批次中找到 >= 20 神器的种子，建议放宽条件或再次尝试！");
                Console.ResetColor();
            }

            Console.WriteLine("\n按任意键返回主菜单...");
            Console.ReadKey();
        }

        private static void ShowGlossaryFlow()
        {
            Console.Clear();
            ConsoleFormatter.PrintHeader("📖 神器与设施对照名鉴");
            Console.WriteLine();

            var trans = PathosContext.Instance.Translations;
            var artifacts = trans.GetAllArtifactTranslations();

            Console.WriteLine("⭐ 常用神器中英文对照示例 (输入中文或英文均可作为筛选关键字):");
            Console.WriteLine(new string('-', 70));

            int count = 0;
            foreach (var (en, cn) in artifacts.Take(30))
            {
                Console.WriteLine($"  • {cn,-20} | {en}");
                count++;
                if (count % 15 == 0 && count < artifacts.Count)
                {
                    Console.WriteLine(new string('-', 70));
                }
            }

            Console.WriteLine(new string('-', 70));
            Console.WriteLine("按任意键返回主菜单...");
            Console.ReadKey();
        }

        private static void ShowEnvironmentFlow()
        {
            Console.Clear();
            ConsoleFormatter.PrintHeader("⚙️ 系统与安装环境信息");
            Console.WriteLine();
            var ctx = PathosContext.Instance;
            Console.WriteLine($"📂 游戏安装路径: {ctx.InstallationPath}");
            Console.WriteLine($"📚 战役模块: {ctx.Campaign.Name} ({ctx.NethackModule.Name})");
            Console.WriteLine($"🌐 中文词典加载状态: {(ctx.Translations.IsLoaded ? "已就绪" : "未加载")}");
            Console.WriteLine($"⚡ 运行平台: .NET {Environment.Version} on {Environment.OSVersion}");
            Console.WriteLine();
            Console.WriteLine("按任意键返回主菜单...");
            Console.ReadKey();
        }
    }
}
