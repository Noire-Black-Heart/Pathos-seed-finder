using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace PathosSeedFinder.Core
{
    public static class TileService
    {
        private static readonly ConcurrentDictionary<string, Dictionary<string, byte[]>> AtlasCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object LoadLock = new();

        public static byte[]? GetTile(string glyphName, string? tileset = null)
        {
            if (string.IsNullOrWhiteSpace(glyphName)) return null;

            string set = string.IsNullOrWhiteSpace(tileset)
                ? (SettingsManager.Load().Tileset ?? "sonyvanda")
                : tileset.Trim().ToLowerInvariant();

            if (set != "classic" && set != "geoduck" && set != "absurd" && set != "sonyvanda")
            {
                set = "sonyvanda";
            }

            // 1. Try loading from atlas cache
            var cache = GetOrLoadAtlas(set);
            if (cache != null)
            {
                string key = glyphName.Trim().ToLowerInvariant();
                if (cache.TryGetValue(key, out var bytes)) return bytes;

                string cleanKey = key.Replace("'", "").Replace("’", "").Replace("_", " ").Trim();
                if (cache.TryGetValue(cleanKey, out bytes)) return bytes;
            }

            // 2. Fallback to loose PNG files
            byte[]? loose = TryGetLooseTile(set, glyphName);
            if (loose != null) return loose;

            // 3. Fallback to sonyvanda / classic if requested tileset didn't have it
            if (set != "sonyvanda")
            {
                var fallbackCache = GetOrLoadAtlas("sonyvanda");
                if (fallbackCache != null)
                {
                    string key = glyphName.Trim().ToLowerInvariant();
                    if (fallbackCache.TryGetValue(key, out var bytes)) return bytes;
                    string cleanKey = key.Replace("'", "").Replace("’", "").Replace("_", " ").Trim();
                    if (fallbackCache.TryGetValue(cleanKey, out bytes)) return bytes;
                }
            }

            return null;
        }

        private static Dictionary<string, byte[]>? GetOrLoadAtlas(string tileset)
        {
            if (AtlasCache.TryGetValue(tileset, out var existing))
            {
                return existing;
            }

            lock (LoadLock)
            {
                if (AtlasCache.TryGetValue(tileset, out existing))
                {
                    return existing;
                }

                string gameDir = PathosContext.Instance?.InstallationPath ?? SettingsManager.Load().PathosDirectory;
                if (!string.IsNullOrEmpty(gameDir))
                {
                    string atlasFile = Path.Combine(gameDir, "Assets", $"{tileset}.Atlas");
                    string indexFile = Path.Combine(gameDir, "Assets", "Atlas.Index");

                    if (File.Exists(atlasFile) && File.Exists(indexFile))
                    {
                        var dict = LoadAtlasFile(atlasFile, indexFile);
                        AtlasCache[tileset] = dict;
                        return dict;
                    }
                }

                return null;
            }
        }

        private static Dictionary<string, byte[]> LoadAtlasFile(string atlasPath, string indexPath)
        {
            var dict = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var names = File.ReadAllLines(indexPath);

            using var fs = File.OpenRead(atlasPath);

            // Determine if the file is in modern VarInt ZigZag format or legacy fixed Int32 format
            bool isVarInt = DetectVarIntFormat(fs);
            fs.Position = 0;

            int count = ReadInt32Auto(fs, isVarInt);
            var sprites = new List<List<byte[]?>>(count);

            for (int i = 0; i < count; i++)
            {
                int frameCountByte = fs.ReadByte();
                if (frameCountByte == -1) break;
                byte frameCount = (byte)frameCountByte;

                var frames = new List<byte[]?>(frameCount);
                for (int f = 0; f < frameCount; f++) frames.Add(null);
                sprites.Add(frames);

                string name = i < names.Length ? names[i] : $"Sprite_{i}";

                for (int f = 0; f < frameCount; f++)
                {
                    int lengthOrRef = ReadInt32Auto(fs, isVarInt);
                    if (lengthOrRef < 0)
                    {
                        int targetSprite = -lengthOrRef - 1;
                        int targetFrame = fs.ReadByte();
                        if (targetSprite >= 0 && targetSprite < sprites.Count && targetFrame >= 0 && targetFrame < sprites[targetSprite].Count)
                        {
                            frames[f] = sprites[targetSprite][targetFrame];
                        }
                    }
                    else
                    {
                        byte[] png = new byte[lengthOrRef];
                        int bytesRead = 0;
                        while (bytesRead < lengthOrRef)
                        {
                            int read = fs.Read(png, bytesRead, lengthOrRef - bytesRead);
                            if (read <= 0) break;
                            bytesRead += read;
                        }
                        frames[f] = png;
                    }
                }

                if (frames.Count > 0 && frames[0] != null)
                {
                    dict[name.ToLowerInvariant()] = frames[0]!;
                    string clean = name.ToLowerInvariant().Replace("'", "").Replace("’", "").Replace("_", " ").Trim();
                    dict[clean] = frames[0]!;
                }
            }

            return dict;
        }

        private static bool DetectVarIntFormat(Stream stream)
        {
            byte[] preview = new byte[16];
            int read = stream.Read(preview, 0, preview.Length);
            if (read < 8) return false;

            // Check if legacy fixed 4-byte Int32 header produces a valid sprite count
            int fixedCount = BitConverter.ToInt32(preview, 0);
            if (fixedCount > 0 && fixedCount < 50000)
            {
                byte frames = preview[4];
                if (frames > 0 && frames <= 64)
                {
                    int len = BitConverter.ToInt32(preview, 5);
                    if (len > 0 && len < 20_000_000)
                    {
                        // Check for PNG header at offset 9
                        if (read >= 13 && preview[9] == 0x89 && preview[10] == 0x50 && preview[11] == 0x4E && preview[12] == 0x47)
                        {
                            return false; // Legacy fixed Int32 format
                        }
                    }
                }
            }

            return true; // Modern Pathos VarInt ZigZag format
        }

        private static int ReadInt32Auto(Stream stream, bool isVarInt)
        {
            if (!isVarInt)
            {
                Span<byte> buf = stackalloc byte[4];
                int read = 0;
                while (read < 4)
                {
                    int r = stream.Read(buf.Slice(read, 4 - read));
                    if (r <= 0) throw new EndOfStreamException();
                    read += r;
                }
                return BitConverter.ToInt32(buf);
            }

            ulong num = 0UL;
            for (int i = 0; i < 64; i += 7)
            {
                int b = stream.ReadByte();
                if (b == -1) throw new EndOfStreamException();
                num |= (ulong)((long)(b & 0x7F) << i);
                if (b < 128)
                {
                    uint u = (uint)num;
                    return (int)((u >> 1) ^ (0 - (u & 1)));
                }
            }
            throw new InvalidDataException("Malformed variable length integer in atlas.");
        }

        private static byte[]? TryGetLooseTile(string tileset, string glyphName)
        {
            string clean = glyphName.Trim();
            string cleanNoApos = clean.Replace("'", "").Replace("’", "");

            string gameDir = PathosContext.Instance?.InstallationPath ?? SettingsManager.Load().PathosDirectory;
            if (string.IsNullOrEmpty(gameDir)) return null;

            // 1. Search in exact tileset subdirectories first
            string[] tilesetDirs = new[]
            {
                Path.Combine(gameDir, "Atlases", tileset),
                Path.Combine(gameDir, "Assets", tileset)
            };

            foreach (var dir in tilesetDirs)
            {
                if (!Directory.Exists(dir)) continue;

                string p1 = Path.Combine(dir, $"{clean}.png");
                if (File.Exists(p1)) return File.ReadAllBytes(p1);

                string p2 = Path.Combine(dir, $"{cleanNoApos}.png");
                if (File.Exists(p2)) return File.ReadAllBytes(p2);
            }

            // 2. Fallback to generic loose files only if tileset is sonyvanda or absurd
            if (tileset == "sonyvanda" || tileset == "absurd")
            {
                string[] genericDirs = new[]
                {
                    Path.Combine(gameDir, "Atlases"),
                    Path.Combine(gameDir, "Assets")
                };

                foreach (var dir in genericDirs)
                {
                    if (!Directory.Exists(dir)) continue;

                    string p1 = Path.Combine(dir, $"{clean}.png");
                    if (File.Exists(p1)) return File.ReadAllBytes(p1);

                    string p2 = Path.Combine(dir, $"{cleanNoApos}.png");
                    if (File.Exists(p2)) return File.ReadAllBytes(p2);
                }
            }

            return null;
        }

        public static void ClearCache()
        {
            lock (LoadLock)
            {
                AtlasCache.Clear();
            }
        }
    }
}
