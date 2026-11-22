using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PathosSeedFinder.Core
{
    public class SeedFilter
    {
        public static void EnsureRules(FilterCriteria criteria)
        {
            if (criteria.Rules != null && criteria.Rules.Count > 0)
            {
                return;
            }

            criteria.Rules = new List<ConditionRule>();

            if (criteria.MinArtifacts.HasValue)
            {
                criteria.Rules.Add(new ConditionRule
                {
                    Type = "artifact_count",
                    MinArtifacts = criteria.MinArtifacts.Value
                });
            }

            if (!string.IsNullOrWhiteSpace(criteria.RequiredArtifact) || criteria.RequiredLevel.HasValue || !string.IsNullOrWhiteSpace(criteria.RequiredSite) || criteria.RequiredArtifactAllowCarried.HasValue)
            {
                criteria.Rules.Add(new ConditionRule
                {
                    Type = "artifact",
                    ArtifactName = criteria.RequiredArtifact,
                    MaxLevel = criteria.RequiredLevel,
                    Site = criteria.RequiredSite,
                    AllowCarried = criteria.RequiredArtifactAllowCarried ?? true
                });
            }

            if (!string.IsNullOrWhiteSpace(criteria.MinetownState))
            {
                criteria.Rules.Add(new ConditionRule
                {
                    Type = "minetown",
                    MinetownState = criteria.MinetownState
                });
            }

            if (!string.IsNullOrWhiteSpace(criteria.ElfKingdomState))
            {
                criteria.Rules.Add(new ConditionRule
                {
                    Type = "elf",
                    ElfKingdomState = criteria.ElfKingdomState
                });
            }

            if (!string.IsNullOrWhiteSpace(criteria.RequiredSokobanPrize) || criteria.RequiredSokobanLevel.HasValue)
            {
                criteria.Rules.Add(new ConditionRule
                {
                    Type = "sokoban",
                    SokobanPrize = criteria.RequiredSokobanPrize,
                    SokobanLevel = criteria.RequiredSokobanLevel
                });
            }

            if (!string.IsNullOrWhiteSpace(criteria.RequiredShop) || criteria.RequiredShopMaxLevel.HasValue || !string.IsNullOrWhiteSpace(criteria.RequiredShopSite) || criteria.MinShops.HasValue)
            {
                criteria.Rules.Add(new ConditionRule
                {
                    Type = "shop",
                    ShopType = criteria.RequiredShop,
                    MaxLevel = criteria.RequiredShopMaxLevel,
                    Site = criteria.RequiredShopSite,
                    MinShops = criteria.MinShops
                });
            }
        }

        public static (bool IsMatch, List<string> MatchedReasons) EvaluateReport(SeedReport report, FilterCriteria criteria)
        {
            EnsureRules(criteria);

            var reasons = new List<string>();
            if (criteria.Rules.Count == 0)
            {
                return (true, reasons);
            }

            bool isAnyMode = string.Equals(criteria.MatchMode, "any", StringComparison.OrdinalIgnoreCase);

            if (isAnyMode)
            {
                bool anyMatched = false;
                foreach (var rule in criteria.Rules)
                {
                    var (matched, reason) = EvaluateSingleRule(report, rule);
                    if (matched)
                    {
                        anyMatched = true;
                        if (!string.IsNullOrEmpty(reason))
                        {
                            reasons.Add(reason);
                        }
                    }
                }
                return (anyMatched, reasons);
            }

            // 混合逻辑 / 默认 ALL 模式：必选 (Must) 全满足 + 备选 (Optional) 任中其一
            var mustRules = criteria.Rules.Where(r => !string.Equals(r.LogicRole, "optional", StringComparison.OrdinalIgnoreCase)).ToList();
            var optionalRules = criteria.Rules.Where(r => string.Equals(r.LogicRole, "optional", StringComparison.OrdinalIgnoreCase)).ToList();

            // 1. 验证所有必选条件 (必须全部满足)
            foreach (var rule in mustRules)
            {
                var (matched, reason) = EvaluateSingleRule(report, rule);
                if (!matched)
                {
                    return (false, new List<string>());
                }
                if (!string.IsNullOrEmpty(reason))
                {
                    reasons.Add(reason);
                }
            }

            // 2. 验证备选条件池 (若有配置，则必须至少满足一个)
            if (optionalRules.Count > 0)
            {
                bool anyOptionalMatched = false;
                bool isEn = string.Equals(SettingsManager.Load().Language, "en", StringComparison.OrdinalIgnoreCase);
                string optTag = isEn ? "[Optional] " : "[备选] ";

                foreach (var rule in optionalRules)
                {
                    var (matched, reason) = EvaluateSingleRule(report, rule);
                    if (matched)
                    {
                        anyOptionalMatched = true;
                        if (!string.IsNullOrEmpty(reason))
                        {
                            reasons.Add($"{optTag}{reason}");
                        }
                    }
                }

                if (!anyOptionalMatched)
                {
                    return (false, new List<string>());
                }
            }

            return (true, reasons);
        }

        public static (bool Matched, string? Reason) EvaluateSingleRule(SeedReport report, ConditionRule rule)
        {
            var trans = PathosContext.Instance.Translations;
            string type = rule.Type?.ToLowerInvariant() ?? "artifact";

            switch (type)
            {
                case "artifact":
                {
                    string reqArt = (rule.ArtifactName ?? "").Trim();
                    string reqEn = string.IsNullOrEmpty(reqArt) ? "" : trans.ResolveItemEnglish(reqArt);
                    string reqSite = (rule.Site ?? "").Trim();
                    int? maxLevel = rule.MaxLevel;

                    foreach (var art in report.Artifacts)
                    {
                        if (!string.IsNullOrEmpty(reqArt))
                        {
                            bool nameMatches = art.EnglishName.Equals(reqEn, StringComparison.OrdinalIgnoreCase) ||
                                               art.ChineseName.Equals(reqArt, StringComparison.OrdinalIgnoreCase) ||
                                               art.EnglishName.Contains(reqEn, StringComparison.OrdinalIgnoreCase) ||
                                               art.ChineseName.Contains(reqArt, StringComparison.OrdinalIgnoreCase);
                            if (!nameMatches) continue;
                        }

                        if (!rule.AllowCarried)
                        {
                            if (art.Possession == PossessionType.Carried)
                            {
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(reqSite))
                        {
                            bool isDungeonReq = reqSite.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                                reqSite.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                                reqSite.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                            bool isArtDungeon = art.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                                art.SiteChinese.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                                art.SiteChinese.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                            bool siteMatches = isDungeonReq ? isArtDungeon :
                                (art.Site.Contains(reqSite, StringComparison.OrdinalIgnoreCase) ||
                                 art.SiteChinese.Contains(reqSite, StringComparison.OrdinalIgnoreCase) ||
                                 (!string.IsNullOrEmpty(art.SubArea) && art.SubArea.Contains(reqSite, StringComparison.OrdinalIgnoreCase)));

                            if (!siteMatches) continue;
                        }

                        if (maxLevel.HasValue)
                        {
                            bool isDungeonReq = string.IsNullOrEmpty(reqSite) ||
                                                reqSite.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                                reqSite.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                                reqSite.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                            bool levelMatches;
                            if (!isDungeonReq)
                            {
                                levelMatches = (art.LevelIndex <= maxLevel.Value || art.DungeonLevel <= maxLevel.Value);
                            }
                            else
                            {
                                levelMatches = (art.DungeonLevel <= maxLevel.Value);
                            }

                            if (!levelMatches) continue;
                        }

                        string artTitle = string.IsNullOrEmpty(art.ChineseName) ? art.EnglishName : art.ChineseName;
                        string locStr = $"{art.SiteChinese} {art.LevelIndex}层";
                        return (true, $"✓ {artTitle} ({locStr})");
                    }

                    return (false, null);
                }

                case "minetown":
                {
                    if (report.Minetown == null) return (false, null);
                    string state = (rule.MinetownState ?? "").Trim().ToLowerInvariant();

                    if (state.Contains("orc") || state.Contains("兽人") || state.Contains("攻城") || state.Contains("屠城"))
                    {
                        if (report.Minetown.IsOrcSiege)
                            return (true, "✓ 矿镇兽人攻城");
                        return (false, null);
                    }
                    else if (state.Contains("normal") || state.Contains("正常") || state.Contains("和平") || state.Contains("守卫"))
                    {
                        if (!report.Minetown.IsOrcSiege)
                            return (true, "✓ 矿镇秩序井然");
                        return (false, null);
                    }
                    return (true, "✓ 矿镇已生成");
                }

                case "elf":
                {
                    if (report.ElfKingdom == null) return (false, null);
                    string state = (rule.ElfKingdomState ?? "").Trim().ToLowerInvariant();

                    if (state.Contains("massacre") || state.Contains("orc") || state.Contains("屠城") || state.Contains("惨案") || state.Contains("暴动") || state.Contains("敌对"))
                    {
                        if (report.ElfKingdom.IsMassacre)
                            return (true, "✓ 精灵王国屠城惨案");
                        return (false, null);
                    }
                    else if (state.Contains("normal") || state.Contains("peaceful") || state.Contains("正常") || state.Contains("和平") || state.Contains("盛世") || state.Contains("繁荣"))
                    {
                        if (!report.ElfKingdom.IsMassacre)
                            return (true, "✓ 精灵王国盛世繁荣");
                        return (false, null);
                    }
                    return (true, "✓ 精灵王国已生成");
                }

                case "sokoban":
                {
                    if (report.Sokoban == null) return (false, null);

                    if (rule.SokobanLevel.HasValue && report.Sokoban.DungeonEntranceLevel != rule.SokobanLevel.Value)
                    {
                        return (false, null);
                    }

                    if (!string.IsNullOrWhiteSpace(rule.SokobanPrize))
                    {
                        string req = rule.SokobanPrize.Trim();
                        string reqEn = trans.ResolveItemEnglish(req);

                        bool MatchesPrize(SokobanRewardInfo? info)
                        {
                            if (info == null) return false;
                            return info.EnglishName.Contains(reqEn, StringComparison.OrdinalIgnoreCase) ||
                                   info.ChineseName.Contains(req, StringComparison.OrdinalIgnoreCase) ||
                                   info.EnglishName.Contains(req, StringComparison.OrdinalIgnoreCase);
                        }

                        string foundPrize = "";
                        if (MatchesPrize(report.Sokoban.FinalBag)) foundPrize = report.Sokoban.FinalBag!.ChineseName;
                        else if (MatchesPrize(report.Sokoban.FinalCloak)) foundPrize = report.Sokoban.FinalCloak!.ChineseName;
                        else if (MatchesPrize(report.Sokoban.FinalAmulet)) foundPrize = report.Sokoban.FinalAmulet!.ChineseName;
                        else if (MatchesPrize(report.Sokoban.FinalRing)) foundPrize = report.Sokoban.FinalRing!.ChineseName;
                        else if (MatchesPrize(report.Sokoban.FinalWand)) foundPrize = report.Sokoban.FinalWand!.ChineseName;
                        else
                        {
                            var fl = report.Sokoban.FloorRewards.FirstOrDefault(MatchesPrize);
                            if (fl != null) foundPrize = fl.ChineseName;
                        }

                        if (!string.IsNullOrEmpty(foundPrize))
                        {
                            return (true, $"✓ 仓库番大奖: {foundPrize}");
                        }
                        return (false, null);
                    }

                    return (true, $"✓ 仓库番 (入口第{report.Sokoban.DungeonEntranceLevel}层)");
                }

                case "shop":
                {
                    string reqShop = (rule.ShopType ?? "").Trim();
                    string reqShopEn = string.IsNullOrEmpty(reqShop) ? "" : trans.ResolveShopEnglish(reqShop);

                    bool hasFilter = !string.IsNullOrEmpty(reqShop) || rule.MaxLevel.HasValue || !string.IsNullOrWhiteSpace(rule.Site);

                    if (hasFilter)
                    {
                        var candidates = report.Shops.Where(s =>
                        {
                            if (!string.IsNullOrEmpty(reqShop))
                            {
                                bool matchName = s.EnglishType.Contains(reqShopEn, StringComparison.OrdinalIgnoreCase) ||
                                                 s.ChineseType.Contains(reqShop, StringComparison.OrdinalIgnoreCase) ||
                                                 s.EnglishType.Contains(reqShop, StringComparison.OrdinalIgnoreCase);
                                if (!matchName) return false;
                            }

                            if (!string.IsNullOrWhiteSpace(rule.Site))
                            {
                                string reqSite = rule.Site.Trim();
                                bool isDungeonReq = reqSite.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                                    reqSite.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                                    reqSite.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                                bool isShopDungeon = s.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase) ||
                                                     s.SiteChinese.Equals("地牢", StringComparison.OrdinalIgnoreCase) ||
                                                     s.SiteChinese.Equals("主地牢", StringComparison.OrdinalIgnoreCase);

                                bool matchSite = isDungeonReq ? isShopDungeon :
                                    (s.Site.Equals(reqSite, StringComparison.OrdinalIgnoreCase) ||
                                     s.SiteChinese.Equals(reqSite, StringComparison.OrdinalIgnoreCase) ||
                                     s.Site.Contains(reqSite, StringComparison.OrdinalIgnoreCase) ||
                                     s.SiteChinese.Contains(reqSite, StringComparison.OrdinalIgnoreCase));

                                if (!matchSite) return false;
                            }

                            if (rule.MaxLevel.HasValue)
                            {
                                int levelToCheck = s.DungeonLevel > 0 ? s.DungeonLevel : s.LevelIndex;
                                if (levelToCheck > rule.MaxLevel.Value)
                                {
                                    return false;
                                }
                            }

                            return true;
                        }).ToList();

                        if (candidates.Count > 0)
                        {
                            var best = candidates.OrderBy(s => s.DungeonLevel > 0 ? s.DungeonLevel : s.LevelIndex).First();
                            int lvl = best.DungeonLevel > 0 ? best.DungeonLevel : best.LevelIndex;
                            string locStr = (best.SiteChinese == "主地牢" || best.SiteChinese == "地牢") ? $"{best.SiteChinese} {lvl}层" : $"{best.SiteChinese} (折算主地牢 {lvl}层)";
                            return (true, $"✓ {best.ChineseType} ({locStr})");
                        }

                        return (false, null);
                    }

                    if (rule.MinShops.HasValue)
                    {
                        if (report.TotalShops >= rule.MinShops.Value)
                        {
                            return (true, $"✓ 全局设施 >= {rule.MinShops.Value}处 (实得{report.TotalShops}处)");
                        }
                        return (false, null);
                    }

                    return (true, null);
                }

                case "artifact_count":
                {
                    if (rule.MinArtifacts.HasValue)
                    {
                        if (report.TotalArtifacts >= rule.MinArtifacts.Value)
                        {
                            return (true, $"✓ 全局神器 >= {rule.MinArtifacts.Value}件 (共{report.TotalArtifacts}件)");
                        }
                        return (false, null);
                    }
                    return (true, null);
                }

                default:
                    return (true, null);
            }
        }

        public static bool Matches(SeedReport report, FilterCriteria criteria)
        {
            var (matched, reasons) = EvaluateReport(report, criteria);
            report.MatchedReasons = reasons;
            return matched;
        }

        public static IEnumerable<SeedReport> Search(
            FilterCriteria criteria,
            Action<int, string, SeedReport?>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            int start = criteria.StartSeedNumber ?? Random.Shared.Next(1, 0x7FFFFFFF);
            int count = criteria.MaxSearchCount;

            for (int i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                int currentNum = start + i;
                string seedStr = criteria.SeedPrefix == "#" 
                    ? $"#{currentNum:X8}" 
                    : $"{criteria.SeedPrefix}{currentNum}";

                SeedReport report;
                try
                {
                    report = SeedAnalyzer.Analyze(seedStr);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error analyzing seed '{seedStr}': {ex.Message}");
                    continue;
                }

                bool match = Matches(report, criteria);
                onProgress?.Invoke(i + 1, seedStr, match ? report : null);

                if (match)
                {
                    yield return report;
                }
            }
        }

        public static async IAsyncEnumerable<SeedReport> SearchParallelAsync(
            FilterCriteria criteria,
            int workerCount,
            Action<int, string, SeedReport?, double>? onProgress = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureRules(criteria);
            var settings = SettingsManager.Load();
            string pathosDir = settings.PathosDirectory;
            if (string.IsNullOrWhiteSpace(pathosDir) || !Directory.Exists(pathosDir))
            {
                yield break;
            }

            int concurrency = Math.Clamp(workerCount, 1, WorkerPool.MaxCores);
            int start = criteria.StartSeedNumber ?? Random.Shared.Next(1, 0x7FFFFFFF);
            int totalCount = criteria.MaxSearchCount;

            var channel = Channel.CreateUnbounded<SeedReport>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            int testedCount = 0;
            var sw = Stopwatch.StartNew();

            var producerTask = Task.Run(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = cts.Token;

                try
                {
                    var seedNumbers = Enumerable.Range(0, totalCount).Select(i => start + i).ToList();

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = concurrency,
                        CancellationToken = token
                    };

                    await Parallel.ForEachAsync(seedNumbers, parallelOptions, async (num, ct) =>
                    {
                        if (ct.IsCancellationRequested) return;

                        string seedStr = criteria.SeedPrefix == "#"
                            ? $"#{num:X8}"
                            : $"{criteria.SeedPrefix}{num}";

                        PathosContext worker = await WorkerPool.RentAsync(pathosDir, ct);
                        SeedReport? report = null;
                        try
                        {
                            report = SeedAnalyzer.Analyze(seedStr, worker);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PARALLEL] Error on seed {seedStr}: {ex.Message}");
                        }
                        finally
                        {
                            WorkerPool.Return(worker);
                        }

                        int currentTested = Interlocked.Increment(ref testedCount);
                        double elapsedSec = Math.Max(0.001, sw.ElapsedMilliseconds / 1000.0);
                        double currentSpeed = currentTested / elapsedSec;

                        bool match = false;
                        if (report != null)
                        {
                            match = Matches(report, criteria);
                            if (match)
                            {
                                await channel.Writer.WriteAsync(report, ct);
                            }
                        }

                        onProgress?.Invoke(currentTested, seedStr, match ? report : null, currentSpeed);
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PARALLEL] Exception in search producer: {ex.Message}");
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            await foreach (var matchReport in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return matchReport;
            }

            await producerTask;
        }
    }
}
