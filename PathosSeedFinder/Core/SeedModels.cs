using System;
using System.Collections.Generic;

namespace PathosSeedFinder.Core
{
    public enum PossessionType
    {
        Ground,
        Container,
        Carried,
        Covered
    }

    public record MapLocation(string Site, string SiteChinese, int LevelIndex, string SubArea);

    public class ArtifactInfo
    {
        public string EnglishName { get; set; } = "";
        public string ChineseName { get; set; } = "";
        public string Glyph { get; set; } = "";
        public string Site { get; set; } = "";
        public string SiteChinese { get; set; } = "";
        public string SubArea { get; set; } = "";
        public string MapRawName { get; set; } = "";
        public int LevelIndex { get; set; }
        public int DungeonLevel { get; set; }
        public PossessionType Possession { get; set; }
        public string HolderName { get; set; } = "";
        public string HolderChinese { get; set; } = "";
        public bool IsLocked { get; set; }
        public bool IsTrapped { get; set; }
        public string CoverBlockName { get; set; } = "";
        public string CoverBlockChinese { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }

        public string DisplayName => string.IsNullOrEmpty(ChineseName) || ChineseName == EnglishName
            ? EnglishName
            : $"{ChineseName} ({EnglishName})";

        public string LocationSummary
        {
            get
            {
                string sub = string.IsNullOrEmpty(SubArea) ? "" : $" [{SubArea}]";
                if (Site.Equals("Dungeon", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{SiteChinese} 第{LevelIndex}层{sub} (Level {LevelIndex})";
                }
                return $"{SiteChinese} 第{LevelIndex}层{sub} [折算主地牢第{DungeonLevel}层] (Level {LevelIndex})";
            }
        }

        public string PossessionSummary
        {
            get
            {
                string coord = $"坐标:({X},{Y})";
                switch (Possession)
                {
                    case PossessionType.Carried:
                        string carrier = string.IsNullOrEmpty(HolderChinese) ? HolderName : $"{HolderChinese} ({HolderName})";
                        return $"[携带] 携带者: {carrier} {coord}";

                    case PossessionType.Container:
                        string cont = string.IsNullOrEmpty(HolderChinese) ? HolderName : $"{HolderChinese} ({HolderName})";
                        string flags = (IsLocked ? " [已上锁]" : "") + (IsTrapped ? " [有陷阱]" : "");
                        string coverSuffix = !string.IsNullOrEmpty(CoverBlockName)
                            ? $" (被 {(string.IsNullOrEmpty(CoverBlockChinese) ? CoverBlockName : $"{CoverBlockChinese} ({CoverBlockName})")} 压覆)"
                            : "";
                        return $"[容器] 藏于: {cont}{flags}{coverSuffix} {coord}";

                    case PossessionType.Covered:
                        string cover = string.IsNullOrEmpty(CoverBlockChinese) ? CoverBlockName : $"{CoverBlockChinese} ({CoverBlockName})";
                        return $"[压覆] 藏于: {cover} 之下 {coord}";

                    default:
                        return $"[地面] 散落在地上 {coord}";
                }
            }
        }
    }

    public class MinetownInfo
    {
        public string MapName { get; set; } = "";
        public string Site { get; set; } = "Mines";
        public string SiteChinese { get; set; } = "矿洞";
        public int LevelIndex { get; set; } = 1;
        public bool IsOrcSiege { get; set; }
        public string StatusDescription { get; set; } = "";
        public int WatchmenCount { get; set; }
        public int OrcCount { get; set; }
        public int MerchantCount { get; set; }
        public bool HasWater { get; set; }
        public bool HasLava { get; set; }
    }

    public class ElfKingdomInfo
    {
        public string MapName { get; set; } = "Elf Forest";
        public string Site { get; set; } = "Elf Kingdom";
        public string SiteChinese { get; set; } = "精灵王国";
        public int DungeonEntranceLevel { get; set; }
        public bool IsMassacre { get; set; }
        public string StatusDescription { get; set; } = "";
        public int TotalHighElfCount { get; set; }
        public int TownHighElfCount { get; set; }
        public int PalaceHighElfCount { get; set; }
        public int OrcCount { get; set; }
        public string KingState { get; set; } = "";
        public string ArtifactState { get; set; } = "";
    }

    public class SokobanRewardInfo
    {
        public string EnglishName { get; set; } = "";
        public string ChineseName { get; set; } = "";
        public string Glyph { get; set; } = "";
        public string ItemType { get; set; } = ""; // Bag, Cloak, Amulet, Ring, Wand
        public int Floor { get; set; } = 4;
        public int X { get; set; }
        public int Y { get; set; }

        public string DisplayName => string.IsNullOrEmpty(ChineseName) || ChineseName == EnglishName
            ? EnglishName
            : $"{ChineseName} ({EnglishName})";
    }

    public class SokobanInfo
    {
        public int DungeonEntranceLevel { get; set; }
        public SokobanRewardInfo? FinalBag { get; set; }
        public SokobanRewardInfo? FinalCloak { get; set; }
        public SokobanRewardInfo? FinalAmulet { get; set; }
        public SokobanRewardInfo? FinalRing { get; set; }
        public SokobanRewardInfo? FinalWand { get; set; }
        public List<SokobanRewardInfo> FloorRewards { get; set; } = new();
    }

    public class BranchEntranceInfo
    {
        public string BranchName { get; set; } = "";
        public string BranchChinese { get; set; } = "";
        public int DungeonLevel { get; set; }
        public string PortalName { get; set; } = "";
        public string PortalChinese { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public string DestinationMap { get; set; } = "";

        public string DisplayBranch => string.IsNullOrEmpty(BranchChinese) || BranchChinese == BranchName
            ? BranchName
            : $"{BranchChinese} ({BranchName})";

        public string DisplayPortal => string.IsNullOrEmpty(PortalChinese) || PortalChinese == PortalName
            ? PortalName
            : $"{PortalChinese} ({PortalName})";
    }

    public class ShopInfo
    {
        public string EnglishType { get; set; } = "";
        public string ChineseType { get; set; } = "";
        public string Site { get; set; } = "";
        public string SiteChinese { get; set; } = "";
        public string SubArea { get; set; } = "";
        public string MapRawName { get; set; } = "";
        public int LevelIndex { get; set; }
        public int DungeonLevel { get; set; }
        public string MerchantName { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }

        public string DisplayType => string.IsNullOrEmpty(ChineseType) || ChineseType == EnglishType
            ? EnglishType
            : $"{ChineseType} ({EnglishType})";
    }

    public class IdentifiedItemInfo
    {
        public string Series { get; set; } = ""; // potion, scroll, ring, wand, amulet, book, etc.
        public string SeriesChinese { get; set; } = "";
        public string TrueNameEn { get; set; } = "";
        public string TrueNameCn { get; set; } = "";
        public string ApparentNameEn { get; set; } = "";
        public string ApparentNameCn { get; set; } = "";
        public string Glyph { get; set; } = "";

        public string DisplayTrueName => string.IsNullOrEmpty(TrueNameCn) || TrueNameCn == TrueNameEn
            ? TrueNameEn
            : $"{TrueNameCn} ({TrueNameEn})";

        public string DisplayApparentName => string.IsNullOrEmpty(ApparentNameCn) || ApparentNameCn == ApparentNameEn
            ? ApparentNameEn
            : $"{ApparentNameCn} ({ApparentNameEn})";
    }

    public class SeedReport
    {
        public string SeedText { get; set; } = "";
        public int SeedCode { get; set; }
        public long ElapsedMs { get; set; }

        public List<ArtifactInfo> Artifacts { get; set; } = new();
        public MinetownInfo? Minetown { get; set; }
        public ElfKingdomInfo? ElfKingdom { get; set; }
        public SokobanInfo? Sokoban { get; set; }
        public List<BranchEntranceInfo> BranchEntrances { get; set; } = new();
        public List<ShopInfo> Shops { get; set; } = new();
        public List<IdentifiedItemInfo> IdentifiedItems { get; set; } = new();
        public List<string> MatchedReasons { get; set; } = new();

        public int TotalArtifacts => Artifacts.Count;
        public int TotalShops => Shops.Count;
        public int TotalIdentifiedItems => IdentifiedItems.Count;
    }

    public class ConditionRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        // "artifact", "minetown", "elf", "sokoban", "shop", "artifact_count"
        public string Type { get; set; } = "artifact";

        // Logic Role: "must" (必选/AND) or "optional" (备选/OR)
        public string LogicRole { get; set; } = "must";

        // Artifact condition
        public string? ArtifactName { get; set; }
        public int? MaxLevel { get; set; }
        public string? Site { get; set; }
        public bool AllowCarried { get; set; } = true;

        // Minetown condition
        public string? MinetownState { get; set; } // "normal" or "orc"

        // Elf Kingdom condition
        public string? ElfKingdomState { get; set; } // "peaceful" or "massacre"

        // Sokoban condition
        public string? SokobanPrize { get; set; }
        public int? SokobanLevel { get; set; }

        // Shop condition
        public string? ShopType { get; set; }
        public int? MinShops { get; set; }

        // Global artifacts count
        public int? MinArtifacts { get; set; }
    }

    public class FilterCriteria
    {
        public string MatchMode { get; set; } = "all"; // "all" or "any"
        public List<ConditionRule> Rules { get; set; } = new();

        public int? MinArtifacts { get; set; }
        public string? RequiredArtifact { get; set; } // Chinese or English
        public string? RequiredSite { get; set; }     // e.g. "Dungeon", "Mines", etc.
        public int? RequiredLevel { get; set; }       // e.g. 1
        public bool? RequiredArtifactAllowCarried { get; set; } = true;
        public string? MinetownState { get; set; }    // "normal", "orc", or null
        public string? ElfKingdomState { get; set; }  // "peaceful", "massacre", or null
        public int? MinShops { get; set; }
        public string? RequiredShop { get; set; }      // e.g. "antique weapons", "古兵器店"
        public int? RequiredShopMaxLevel { get; set; } // e.g. 3 (<= 3 层)
        public string? RequiredShopSite { get; set; }  // e.g. "Dungeon", "Mines"
        public string? RequiredSokobanPrize { get; set; } // e.g. "bag of holding", "收纳之袋", "reflection", "反射护符"
        public int? RequiredSokobanLevel { get; set; }   // e.g. 6
        public string? RequiredBranch { get; set; }     // e.g. "Fort Ludios", "卢底人堡垒"
        public int? RequiredBranchLevel { get; set; }   // e.g. 10
        public int MaxSearchCount { get; set; } = 500;
        public int? StartSeedNumber { get; set; }
        public string SeedPrefix { get; set; } = "#";
    }
}
