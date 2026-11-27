using System.Collections.Generic;

namespace PathosSeedFinder.Core
{
    public class MapCatalogItem
    {
        public string Id { get; set; } = "";
        public string RawName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Group { get; set; } = "";
        public string Site { get; set; } = "";
        public string SiteChinese { get; set; } = "";
        public int LevelIndex { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class MapTileData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string? Floor { get; set; }
        public string? FloorGlyph { get; set; }
        public string? Wall { get; set; }
        public string? WallSegment { get; set; }
        public string? WallGlyph { get; set; }
        public string? Door { get; set; }
        public string? DoorState { get; set; }
        public string? DoorGlyph { get; set; }
        public bool IsSecretDoor { get; set; }
        public string? Passage { get; set; }
        public string? PassageGlyph { get; set; }
        public string? PassageDest { get; set; }
        public string? Fixture { get; set; }
        public string? FixtureChinese { get; set; }
        public string? FixtureGlyph { get; set; }
        public string? FixtureSanctity { get; set; } // "blessed", "uncursed", "cursed", or null
        public string? Spill { get; set; }
        public string? SpillGlyph { get; set; }
        public string? Boulder { get; set; }
        public string? BoulderGlyph { get; set; }
        public string? Trap { get; set; }
        public string? TrapGlyph { get; set; }
        public bool? TrapRevealed { get; set; }
        public string? PrimaryGlyph { get; set; }
        public string? Tooltip { get; set; }
    }

    public class ShopMarkerData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int GlobalX { get; set; }
        public int GlobalY { get; set; }
        public string ShopType { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? MerchantName { get; set; }
        public string? MerchantGender { get; set; }
        public string Glyph { get; set; } = "";
        public string Tooltip { get; set; } = "";
        public bool IsShrineOrStation { get; set; }
        public int? ZoneLeft { get; set; }
        public int? ZoneTop { get; set; }
        public int? ZoneRight { get; set; }
        public int? ZoneBottom { get; set; }
    }

    public class MapItemData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Name { get; set; } = "";
        public string NameChinese { get; set; } = "";
        public string Glyph { get; set; } = "";
        public string State { get; set; } = "ground"; // "ground", "covered", "carried", "container"
        public bool IsArtifact { get; set; }
        public string? CarrierName { get; set; }
        public string? CarrierChinese { get; set; }
        public string? ContainerName { get; set; }
        public string? ContainerChinese { get; set; }
        public string? BoulderName { get; set; }
        public string? BoulderChinese { get; set; }
        public string? Sanctity { get; set; } // "blessed", "uncursed", "cursed", or null if no sanctity
        public int Count { get; set; } = 1;
    }

    public class FloorMapResponse
    {
        public string SeedText { get; set; } = "";
        public int SeedCode { get; set; }
        public string MapId { get; set; } = "";
        public string MapName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Group { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int BouldersCount { get; set; }
        public int TrapsCount { get; set; }
        public int ItemsCount { get; set; }
        public List<ShopMarkerData> Shops { get; set; } = new();
        public List<MapItemData> Items { get; set; } = new();
        public List<MapTileData> Tiles { get; set; } = new();
    }
}
