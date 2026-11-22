using System;
using System.IO;
using System.Linq;
using System.Text;
using PathosSeedFinder.Core;

namespace PathosSeedFinder.UI
{
    public static class ConsoleFormatter
    {
        public static void PrintReport(SeedReport report)
        {
            Console.WriteLine();
            PrintHeader($"💎 种子深度分析报告 : {report.SeedText} (Code: {report.SeedCode}) 💎");
            Console.WriteLine($"⏱️  生成与解析耗时: {report.ElapsedMs} 毫秒");
            Console.WriteLine(new string('-', 70));

            // 1. Minetown Section
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("【 🏰 矿镇生态状态 (Minetown) 】");
            Console.ResetColor();

            if (report.Minetown != null)
            {
                var mt = report.Minetown;
                if (mt.IsOrcSiege)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ⚠️  状态: 【兽人攻城/屠城惨案】(Orcish Massacre)");
                    Console.ResetColor();
                    Console.WriteLine($"     说明: 城镇被兽人士兵占领，原守卫全部阵亡！商店遭到劫掠与破坏。");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  🛡️  状态: 【秩序井然/守卫巡逻】(Peaceful / Guarded)");
                    Console.ResetColor();
                    Console.WriteLine($"     说明: 城镇警卫队正常驻防，治安良好，商店完好无损。");
                }
                Console.WriteLine($"  📍  位置: {mt.SiteChinese} 第{mt.LevelIndex}层 (矿镇) (Level {mt.LevelIndex})");
                Console.WriteLine($"  👥  人口: 警卫队 {mt.WatchmenCount}人 | 兽人 {mt.OrcCount}只 | 商人 {mt.MerchantCount}人");
                if (mt.HasWater) Console.WriteLine($"  🌊  特色: 地形带有水流运河 (Waterway)");
                if (mt.HasLava) Console.WriteLine($"  🌋  特色: 地形带有地下岩浆 (Lavaflow)");
            }
            else
            {
                Console.WriteLine("  未生成常规矿镇。");
            }
            Console.WriteLine();

            // 2. Elf Kingdom Section
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("【 🧝 精灵王国政局生态 (Elf Kingdom) 】");
            Console.ResetColor();

            if (report.ElfKingdom != null)
            {
                var ek = report.ElfKingdom;
                if (ek.IsMassacre)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ⚠️  政局: 【屠城惨案/暴动敌对】(Elf Massacre & Rebellion)");
                    Console.ResetColor();
                    Console.WriteLine($"     说明: 兽人精锐突袭入侵！小镇精灵与商人惨遭血洗屠杀，王宫全员敌对戒备。");
                    Console.WriteLine($"     👑  精灵王: {ek.KingState}");
                    Console.WriteLine($"     🗡️  神器流向: {ek.ArtifactState}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  🕊️  政局: 【盛世繁荣/和平友好】(Prosperous & Peaceful)");
                    Console.ResetColor();
                    Console.WriteLine($"     说明: 高等精灵古国国泰民安，小镇商贸繁华，王宫长桌设宴，氛围和平友好。");
                    Console.WriteLine($"     👑  精灵王: {ek.KingState}");
                    Console.WriteLine($"     ✨  神器流向: {ek.ArtifactState}");
                }

                if (ek.DungeonEntranceLevel > 0)
                {
                    Console.WriteLine($"  🚪  入口位置: 地牢 第{ek.DungeonEntranceLevel}层 (Dungeon Level {ek.DungeonEntranceLevel})");
                }
                Console.WriteLine($"  👥  人口统计: 小镇生还精灵 {ek.TownHighElfCount}人 | 王宫守备精灵 {ek.PalaceHighElfCount}人 | 兽人侵略军 {ek.OrcCount}只");
            }
            else
            {
                Console.WriteLine("  未生成精灵王国。");
            }
            Console.WriteLine();

            // 3. Branch Entrances Section
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("【 🚪 地牢主线各分支入口分布 (Branch Entrances) 】");
            Console.ResetColor();

            if (report.BranchEntrances.Count > 0)
            {
                foreach (var b in report.BranchEntrances)
                {
                    Console.Write($"  • ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{b.DisplayBranch,-22} ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($": 地牢 第{b.DungeonLevel,2}层 ");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write($"坐标:({b.X,2},{b.Y,2}) ");
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"[{b.DisplayPortal}]");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  ↳ 次级衍生分支:");
                Console.WriteLine("    • 地城 (Underdeep)     : 始于 矿洞 第 5 层 (需穿过水晶之门)");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("  （未检测到分支入口数据）");
            }
            Console.WriteLine();

            // 3. Sokoban Section
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("【 📦 仓库番全奖励情报 (Sokoban) 】");
            Console.ResetColor();

            if (report.Sokoban != null)
            {
                var sk = report.Sokoban;
                if (sk.DungeonEntranceLevel > 0)
                {
                    Console.WriteLine($"  🚪  入口位置: 地牢 第{sk.DungeonEntranceLevel}层 (Dungeon Level {sk.DungeonEntranceLevel})");
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  🏆  第 4 层终极宝藏三大件 (Sokoban Big 3):");
                Console.ResetColor();
                if (sk.FinalBag != null)
                {
                    Console.WriteLine($"     • 魔法背包: {sk.FinalBag.DisplayName,-28} 坐标:({sk.FinalBag.X},{sk.FinalBag.Y})");
                }
                if (sk.FinalAmulet != null)
                {
                    Console.WriteLine($"     • 魔法护符: {sk.FinalAmulet.DisplayName,-28} 坐标:({sk.FinalAmulet.X},{sk.FinalAmulet.Y})");
                }
                if (sk.FinalCloak != null)
                {
                    Console.WriteLine($"     • 魔法斗篷: {sk.FinalCloak.DisplayName,-28} 坐标:({sk.FinalCloak.X},{sk.FinalCloak.Y})");
                }

                if (sk.FinalRing != null || sk.FinalWand != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("  ✨  第 4 层随行魔法道具:");
                    Console.ResetColor();
                    if (sk.FinalRing != null)
                        Console.WriteLine($"     • 魔法戒指: {sk.FinalRing.DisplayName,-28} 坐标:({sk.FinalRing.X},{sk.FinalRing.Y})");
                    if (sk.FinalWand != null)
                        Console.WriteLine($"     • 魔法魔杖: {sk.FinalWand.DisplayName,-28} 坐标:({sk.FinalWand.X},{sk.FinalWand.Y})");
                }

                if (sk.FloorRewards.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  📜  1~3 层沿途魔法道具:");
                    Console.ResetColor();
                    var floorGroups = sk.FloorRewards.GroupBy(r => r.Floor).OrderBy(g => g.Key);
                    foreach (var fg in floorGroups)
                    {
                        string items = string.Join(", ", fg.Select(r => r.DisplayName));
                        Console.WriteLine($"     • 第 {fg.Key} 层: {items}");
                    }
                }
            }
            else
            {
                Console.WriteLine("  未生成常规仓库番。");
            }
            Console.WriteLine();

            // 4. Artifacts Section
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"【 🗡️  神器总览与分布 (共 {report.TotalArtifacts} 件神器) 】");
            Console.ResetColor();

            if (report.Artifacts.Count == 0)
            {
                Console.WriteLine("  （本种子中未检测到生成的神器）");
            }
            else
            {
                // Group artifacts by Site
                var grouped = report.Artifacts
                    .OrderBy(a => a.Site)
                    .ThenBy(a => a.LevelIndex)
                    .GroupBy(a => a.SiteChinese);

                foreach (var group in grouped)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  ▶ [{group.Key}] 分布:");
                    Console.ResetColor();

                    foreach (var art in group)
                    {
                        Console.Write($"     • ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{art.DisplayName,-28} ");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        string sub = string.IsNullOrEmpty(art.SubArea) ? "" : $" ({art.SubArea})";
                        string floorInfo = art.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase)
                            ? $"第{art.LevelIndex,2}层{sub} "
                            : $"第{art.LevelIndex,2}层{sub} (折算地牢第{art.DungeonLevel}层) ";
                        Console.Write(floorInfo);
                        Console.ForegroundColor = art.Possession switch
                        {
                            PossessionType.Carried => ConsoleColor.Magenta,
                            PossessionType.Container => ConsoleColor.Cyan,
                            PossessionType.Covered => ConsoleColor.DarkYellow,
                            _ => ConsoleColor.DarkGreen
                        };
                        Console.WriteLine(art.PossessionSummary);
                        Console.ResetColor();
                    }
                }
            }
            Console.WriteLine();

            // 5. Shops & Merchants Section
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"【 🏪 商店与商人情报 (共 {report.TotalShops} 处设施) 】");
            Console.ResetColor();

            if (report.Shops.Count == 0)
            {
                Console.WriteLine("  （本种子中未检测到商店）");
            }
            else
            {
                // Show early shops (Dungeon 1-10, Mines 1-5, etc.)
                var earlyShops = report.Shops
                    .Where(s => (s.Site == "Dungeon" && s.LevelIndex <= 10) || (s.Site == "Mines" && s.LevelIndex <= 3))
                    .OrderBy(s => s.Site)
                    .ThenBy(s => s.LevelIndex)
                    .ToList();

                if (earlyShops.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ⭐ 【前期推荐商店 (地牢1-10层 / 矿洞1-3层)】:");
                    Console.ResetColor();
                    foreach (var s in earlyShops)
                    {
                        string merchant = string.IsNullOrEmpty(s.MerchantName) ? "" : $" (店主: {s.MerchantName})";
                        string sub = string.IsNullOrEmpty(s.SubArea) ? "" : $" ({s.SubArea})";
                        Console.WriteLine($"     • {s.SiteChinese} 第{s.LevelIndex}层{sub}: {s.DisplayType}{merchant} 坐标:({s.X},{s.Y})");
                    }
                    Console.WriteLine();
                }

                // Group all shops by site
                var groupedShops = report.Shops.GroupBy(s => s.SiteChinese);
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("  📋 【全区域商店分布统计】:");
                Console.ResetColor();
                foreach (var g in groupedShops)
                {
                    var shopTypeCounts = g.GroupBy(s => s.ChineseType)
                                          .Select(tg => $"{tg.Key}x{tg.Count()}")
                                          .ToList();
                    Console.WriteLine($"     • {g.Key,-12}: 共 {g.Count()} 处 [{string.Join(", ", shopTypeCounts.Take(4))}{(shopTypeCounts.Count > 4 ? "..." : "")}]");
                }
            }

            // 6. Identified Items Section (Potions & Scrolls Preview)
            if (report.IdentifiedItems.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"【 🧪 全知鉴定表 (共 {report.TotalIdentifiedItems} 项道具外观已破译) 】");
                Console.ResetColor();

                var keyPotions = report.IdentifiedItems.Where(i => i.Series == "potion").Take(6).ToList();
                if (keyPotions.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  🧪 药水外观对应 (部分):");
                    Console.ResetColor();
                    foreach (var p in keyPotions)
                    {
                        Console.WriteLine($"     • {p.DisplayApparentName,-26} ➜  {p.DisplayTrueName}");
                    }
                }

                var keyScrolls = report.IdentifiedItems.Where(i => i.Series == "scroll").Take(6).ToList();
                if (keyScrolls.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("  📜 卷轴外观对应 (部分):");
                    Console.ResetColor();
                    foreach (var s in keyScrolls)
                    {
                        Console.WriteLine($"     • {s.DisplayApparentName,-32} ➜  {s.DisplayTrueName}");
                    }
                }
            }

            Console.WriteLine(new string('=', 70));
        }

        public static string GenerateMarkdownReport(SeedReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Pathos 随机种子报告: `{report.SeedText}`");
            sb.AppendLine();
            sb.AppendLine($"- **种子文本**: `{report.SeedText}`");
            sb.AppendLine($"- **数字代号 (Seed Code)**: `{report.SeedCode}`");
            sb.AppendLine($"- **解析耗时**: {report.ElapsedMs} ms");
            sb.AppendLine();

            sb.AppendLine("## 🏰 矿镇生态状态 (Minetown)");
            sb.AppendLine();
            if (report.Minetown != null)
            {
                var mt = report.Minetown;
                string status = mt.IsOrcSiege ? "🔴 **兽人屠城/攻城状态 (Orcish Massacre)**" : "🟢 **秩序井然/守卫巡逻状态 (Peaceful / Guarded)**";
                sb.AppendLine($"- **当前状态**: {status}");
                sb.AppendLine($"- **所在位置**: {mt.SiteChinese} (Mines) 第 {mt.LevelIndex} 层 (矿镇)");
                sb.AppendLine($"- **人口配置**: 警卫 {mt.WatchmenCount}人 | 兽人 {mt.OrcCount}只 | 商人 {mt.MerchantCount}人");
                if (mt.HasWater) sb.AppendLine($"- **地貌特征**: 包含水流运河 (Waterway)");
                if (mt.HasLava) sb.AppendLine($"- **地貌特征**: 包含地下岩浆 (Lavaflow)");
            }
            else
            {
                sb.AppendLine("未生成常规矿镇。");
            }
            sb.AppendLine();

            sb.AppendLine("## 🧝 精灵王国政局生态 (Elf Kingdom)");
            sb.AppendLine();
            if (report.ElfKingdom != null)
            {
                var ek = report.ElfKingdom;
                string ekStatus = ek.IsMassacre ? "🔴 **屠城惨案/暴动敌对 (Elf Massacre & Rebellion)**" : "🟢 **盛世繁荣/和平友好 (Prosperous & Peaceful)**";
                sb.AppendLine($"- **当前政局**: {ekStatus}");
                if (ek.DungeonEntranceLevel > 0)
                {
                    sb.AppendLine($"- **入口位置**: 地牢 (Dungeon) 第 {ek.DungeonEntranceLevel} 层");
                }
                sb.AppendLine($"- **人口配置**: 小镇生还精灵 {ek.TownHighElfCount}人 | 王宫守备精灵 {ek.PalaceHighElfCount}人 | 兽人侵略军 {ek.OrcCount}只");
                sb.AppendLine($"- **精灵王状态**: {ek.KingState}");
                sb.AppendLine($"- **神器流向**: {ek.ArtifactState}");
            }
            else
            {
                sb.AppendLine("未生成精灵王国。");
            }
            sb.AppendLine();

            sb.AppendLine("## 🚪 地牢主线各分支入口 (Branch Entrances)");
            sb.AppendLine();
            sb.AppendLine("| 分支区域 | 主地牢深度 | 坐标 | 传送门设施 | 目标地图 |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
            foreach (var b in report.BranchEntrances)
            {
                sb.AppendLine($"| **{b.DisplayBranch}** | 地牢 第 {b.DungeonLevel} 层 | ({b.X}, {b.Y}) | {b.DisplayPortal} | {b.DestinationMap} |");
            }
            sb.AppendLine();
            sb.AppendLine("> **次级衍生分支**: 地城 (Underdeep) 始于 **矿洞 第 5 层**（穿过水晶之门进入地下高地）。");
            sb.AppendLine();

            sb.AppendLine("## 📦 仓库番全奖励情报 (Sokoban)");
            sb.AppendLine();
            if (report.Sokoban != null)
            {
                var sk = report.Sokoban;
                if (sk.DungeonEntranceLevel > 0)
                {
                    sb.AppendLine($"- **入口位置**: 地牢 (Dungeon) 第 {sk.DungeonEntranceLevel} 层");
                }
                sb.AppendLine();
                sb.AppendLine("### 🏆 第 4 层终极宝藏三大件");
                sb.AppendLine();
                sb.AppendLine("| 装备类别 | 装备名称 (中文/英文) | 坐标 |");
                sb.AppendLine("| :--- | :--- | :--- |");
                if (sk.FinalBag != null) sb.AppendLine($"| **魔法背包** | {sk.FinalBag.DisplayName} | ({sk.FinalBag.X}, {sk.FinalBag.Y}) |");
                if (sk.FinalAmulet != null) sb.AppendLine($"| **魔法护符** | {sk.FinalAmulet.DisplayName} | ({sk.FinalAmulet.X}, {sk.FinalAmulet.Y}) |");
                if (sk.FinalCloak != null) sb.AppendLine($"| **魔法斗篷** | {sk.FinalCloak.DisplayName} | ({sk.FinalCloak.X}, {sk.FinalCloak.Y}) |");
                if (sk.FinalRing != null) sb.AppendLine($"| **魔法戒指** | {sk.FinalRing.DisplayName} | ({sk.FinalRing.X}, {sk.FinalRing.Y}) |");
                if (sk.FinalWand != null) sb.AppendLine($"| **魔法魔杖** | {sk.FinalWand.DisplayName} | ({sk.FinalWand.X}, {sk.FinalWand.Y}) |");
                sb.AppendLine();

                if (sk.FloorRewards.Count > 0)
                {
                    sb.AppendLine("### 📜 1~3 层沿途魔法道具");
                    sb.AppendLine();
                    sb.AppendLine("| 楼层 | 道具类型 | 道具名称 (中文/英文) | 坐标 |");
                    sb.AppendLine("| :--- | :--- | :--- | :--- |");
                    foreach (var r in sk.FloorRewards.OrderBy(r => r.Floor))
                    {
                        sb.AppendLine($"| 第 {r.Floor} 层 | {r.ItemType} | {r.DisplayName} | ({r.X}, {r.Y}) |");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("未生成常规仓库番。");
                sb.AppendLine();
            }

            sb.AppendLine($"## 🗡️ 神器总览 ({report.TotalArtifacts} 件)");
            sb.AppendLine();
            sb.AppendLine("| 神器名称 (中文/英文) | 区域 | 分支层数 | 折算主地牢深度 | 存放形式 | 详细状态 (携带者/容器/覆物) | 坐标 |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
            foreach (var art in report.Artifacts.OrderBy(a => a.Site).ThenBy(a => a.LevelIndex))
            {
                string pos = art.Possession switch
                {
                    PossessionType.Carried => "怪物携带",
                    PossessionType.Container => "宝箱/容器",
                    PossessionType.Covered => "巨石/木桶压覆",
                    _ => "地面散落"
                };

                string detail = art.Possession switch
                {
                    PossessionType.Carried => string.IsNullOrEmpty(art.HolderChinese) ? art.HolderName : $"{art.HolderChinese} ({art.HolderName})",
                    PossessionType.Container => (string.IsNullOrEmpty(art.HolderChinese) ? art.HolderName : $"{art.HolderChinese} ({art.HolderName})")
                                                + (art.IsLocked ? " [已上锁]" : "")
                                                + (art.IsTrapped ? " [有陷阱]" : "")
                                                + (!string.IsNullOrEmpty(art.CoverBlockName) ? $" (被 {(string.IsNullOrEmpty(art.CoverBlockChinese) ? art.CoverBlockName : $"{art.CoverBlockChinese} ({art.CoverBlockName})")} 压覆)" : ""),
                    PossessionType.Covered => string.IsNullOrEmpty(art.CoverBlockChinese) ? art.CoverBlockName : $"{art.CoverBlockChinese} ({art.CoverBlockName})",
                    _ => "直接散落在地面"
                };

                string sub = string.IsNullOrEmpty(art.SubArea) ? "" : $" ({art.SubArea})";
                string convertedDepth = art.Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase)
                    ? $"第 {art.DungeonLevel} 层"
                    : $"第 {art.DungeonLevel} 层入口";
                sb.AppendLine($"| **{art.DisplayName}** | {art.SiteChinese} ({art.Site}) | 第{art.LevelIndex}层{sub} | {convertedDepth} | {pos} | {detail} | ({art.X}, {art.Y}) |");
            }
            sb.AppendLine();

            sb.AppendLine($"## 🏪 商店与商人分布 ({report.TotalShops} 处)");
            sb.AppendLine();
            sb.AppendLine("| 区域 | 层数 | 商店类型 | 店主名字 | 坐标 |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
            foreach (var s in report.Shops.OrderBy(s => s.Site).ThenBy(s => s.LevelIndex))
            {
                string merchant = string.IsNullOrEmpty(s.MerchantName) ? "-" : s.MerchantName;
                string sub = string.IsNullOrEmpty(s.SubArea) ? "" : $" ({s.SubArea})";
                sb.AppendLine($"| {s.SiteChinese} | 第{s.LevelIndex}层{sub} | {s.DisplayType} | {merchant} | ({s.X}, {s.Y}) |");
            }

            if (report.IdentifiedItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"## 🧪 全知鉴定表 (Identified Items: {report.TotalIdentifiedItems} 项)");
                sb.AppendLine();
                sb.AppendLine("| 类别 | 未鉴定外观 (In-Game Appearance) | 真实身份 (True Identity) | 英文原名 |");
                sb.AppendLine("| :--- | :--- | :--- | :--- |");
                foreach (var item in report.IdentifiedItems)
                {
                    sb.AppendLine($"| {item.SeriesChinese} | **{item.ApparentNameCn}** (`{item.ApparentNameEn}`) | **{item.TrueNameCn}** | `{item.TrueNameEn}` |");
                }
            }

            return sb.ToString();
        }

        public static void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            int width = Math.Max(title.Length + 4, 60);
            string border = new('=', width);
            Console.WriteLine(border);
            int padding = Math.Max(0, (width - title.Length) / 2);
            Console.WriteLine(new string(' ', padding) + title);
            Console.WriteLine(border);
            Console.ResetColor();
        }
    }
}
