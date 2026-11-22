using System;
using System.Collections.Generic;
using System.IO;

namespace PathosSeedFinder.Core
{
    public class TranslationManager
    {
        private readonly Dictionary<string, string> itemEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> itemCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> shopEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> shopCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> entityEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> entityCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> siteEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> siteCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> blockEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> blockCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> portalEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> portalCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> appearanceEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> appearanceCnToEn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> deviceEnToCn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> deviceCnToEn = new(StringComparer.OrdinalIgnoreCase);

        public bool IsLoaded { get; private set; }

        public void Load(string dictPath)
        {
            if (!File.Exists(dictPath))
                return;

            foreach (var line in File.ReadLines(dictPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',', 3);
                if (parts.Length < 3) continue;

                string category = parts[0].Trim();
                string en = parts[1].Trim().Trim('"');
                string cn = parts[2].Trim().Trim('"');

                if (string.IsNullOrEmpty(en) || string.IsNullOrEmpty(cn)) continue;

                switch (category)
                {
                    case "ItemName":
                        itemEnToCn[en] = cn;
                        itemCnToEn[cn] = en;
                        break;
                    case "ShopName":
                    case "ShrineName":
                        shopEnToCn[en] = cn;
                        shopCnToEn[cn] = en;
                        break;
                    case "EntityName":
                        entityEnToCn[en] = cn;
                        entityCnToEn[cn] = en;
                        break;
                    case "SiteName":
                    case "ModuleTerm":
                        siteEnToCn[en] = cn;
                        siteCnToEn[cn] = en;
                        break;
                    case "BlockName":
                        blockEnToCn[en] = cn;
                        blockCnToEn[cn] = en;
                        break;
                    case "PortalName":
                        portalEnToCn[en] = cn;
                        portalCnToEn[cn] = en;
                        break;
                    case "AppearanceName":
                        appearanceEnToCn[en] = cn;
                        appearanceCnToEn[cn] = en;
                        break;
                    case "DeviceName":
                        deviceEnToCn[en] = cn;
                        deviceCnToEn[cn] = en;
                        break;
                }
            }

            // Quest-based synthetic branch names not in dictionary
            siteEnToCn["Underdeep"] = "地城";
            siteCnToEn["地城"] = "Underdeep";

            // Main Dungeon aliases
            siteEnToCn["Dungeon"] = "主地牢";
            siteCnToEn["主地牢"] = "Dungeon";
            siteCnToEn["地牢"] = "Dungeon";

            IsLoaded = true;
        }

        public string TranslateItem(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            return itemEnToCn.TryGetValue(en, out var cn) ? cn : en;
        }

        public string ResolveItemEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (itemCnToEn.TryGetValue(name, out var en)) return en;
            if (itemEnToCn.ContainsKey(name)) return name;

            // 智能别名与民间俗称容错
            string trimmed = name.Trim();
            if (trimmed.Contains("潘多拉", StringComparison.OrdinalIgnoreCase)) return "Pandora's Box";
            if (trimmed.Contains("贪婪法典", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("贪婪", StringComparison.OrdinalIgnoreCase)) return "Miserus";
            if (trimmed.Contains("大师钥匙", StringComparison.OrdinalIgnoreCase)) return "Master Key";
            if (trimmed.Contains("太阳神之眼", StringComparison.OrdinalIgnoreCase)) return "Eyes of Ra";

            return name;
        }

        public string TranslateShop(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            return shopEnToCn.TryGetValue(en, out var cn) ? cn : en;
        }

        public string ResolveShopEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (shopCnToEn.TryGetValue(name, out var en)) return en;
            if (shopEnToCn.ContainsKey(name)) return name;
            return name;
        }

        public string TranslateEntity(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            return entityEnToCn.TryGetValue(en, out var cn) ? cn : en;
        }

        public string TranslateSite(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            string cleaned = en.Trim('_');
            if (cleaned.Equals("Dungeon", StringComparison.OrdinalIgnoreCase)) return "主地牢";
            return siteEnToCn.TryGetValue(cleaned, out var cn) ? cn : cleaned;
        }

        public string ResolveSiteEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (name.Equals("主地牢", StringComparison.OrdinalIgnoreCase) || name.Equals("地牢", StringComparison.OrdinalIgnoreCase)) return "Dungeon";
            if (siteCnToEn.TryGetValue(name, out var en)) return en;
            if (siteEnToCn.ContainsKey(name)) return name;
            return name;
        }

        public string TranslateBlock(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            return blockEnToCn.TryGetValue(en, out var cn) ? cn : en;
        }

        public string ResolveBlockEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (blockCnToEn.TryGetValue(name, out var en)) return en;
            if (blockEnToCn.ContainsKey(name)) return name;
            return name;
        }

        public string TranslatePortal(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            return portalEnToCn.TryGetValue(en, out var cn) ? cn : en;
        }

        public string ResolvePortalEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (portalCnToEn.TryGetValue(name, out var en)) return en;
            if (portalEnToCn.ContainsKey(name)) return name;
            return name;
        }

        public string TranslateAppearance(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            if (appearanceEnToCn.TryGetValue(en, out var cn)) return cn;
            if (itemEnToCn.TryGetValue(en, out cn)) return cn;
            return en;
        }

        public string ResolveAppearanceEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (appearanceCnToEn.TryGetValue(name, out var en)) return en;
            if (appearanceEnToCn.ContainsKey(name)) return name;
            return name;
        }

        public string TranslateTrap(string en)
        {
            if (string.IsNullOrEmpty(en)) return "";
            return deviceEnToCn.TryGetValue(en, out var cn) ? cn : en;
        }

        public string ResolveTrapEnglish(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (deviceCnToEn.TryGetValue(name, out var en)) return en;
            if (deviceEnToCn.ContainsKey(name)) return name;
            return name;
        }

        public IReadOnlyDictionary<string, string> GetAllArtifactTranslations()
        {
            return itemEnToCn;
        }
    }
}
