using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace PathosSeedFinder.Core
{
    public class IsolatedAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string _baseDir;

        public IsolatedAssemblyLoadContext(string baseDir) : base(isCollectible: false)
        {
            _baseDir = baseDir;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string path = Path.Combine(_baseDir, assemblyName.Name + ".dll");
            if (File.Exists(path))
            {
                return LoadFromAssemblyPath(path);
            }
            return null;
        }
    }

    public class PathosContext
    {
        private static readonly object initLock = new();
        private static PathosContext? instance;

        public static PathosContext? Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (initLock)
                    {
                        if (instance == null)
                        {
                            var settings = SettingsManager.Load();
                            if (!string.IsNullOrWhiteSpace(settings.PathosDirectory))
                            {
                                TryInitialize(settings.PathosDirectory, out _);
                            }
                        }
                    }
                }
                return instance;
            }
        }

        public static bool IsInitialized => instance != null && instance.IsReady;

        public bool IsReady { get; private set; }
        public string InstallationPath { get; private set; } = "";
        public string GameVersion { get; private set; } = "";
        public TranslationManager Translations { get; } = new();

        public dynamic Campaign { get; private set; } = null!;
        public dynamic NethackModule { get; private set; } = null!;
        public dynamic Manifest { get; private set; } = null!;

        public Type AdventureType { get; private set; } = null!;
        public Type EngineType { get; private set; } = null!;
        public Type SeedType { get; private set; } = null!;

        private MethodInfo seedFromTextMethod = null!;
        private MethodInfo seedFromCodeMethod = null!;
        private MethodInfo reseedMethod = null!;
        private MethodInfo prepareShuffleMethod = null!;
        private MethodInfo commitShuffleMethod = null!;
        private MethodInfo reverseShuffleMethod = null!;

        public static bool TryInitialize(string? path, out string error)
        {
            lock (initLock)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    var settings = SettingsManager.Load();
                    path = settings.PathosDirectory;
                }

                if (!SettingsManager.ValidatePathosDirectory(path, out error))
                {
                    return false;
                }

                try
                {
                    var ctx = new PathosContext(path);
                    instance = ctx;
                    var settings = SettingsManager.Load();
                    settings.PathosDirectory = path;
                    SettingsManager.Save(settings);
                    error = "";
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"挂载游戏目录失败: {ex.Message}";
                    return false;
                }
            }
        }

        public static PathosContext CreateIsolated(string path)
        {
            var alc = new IsolatedAssemblyLoadContext(path);
            return new PathosContext(path, alc);
        }

        public PathosContext(string path, AssemblyLoadContext? alc = null)
        {
            InstallationPath = path;

            // Load translation dictionary
            string dictPath = Path.Combine(InstallationPath, "Assets", "zh-Simplified.Dictionary");
            if (File.Exists(dictPath))
            {
                Translations.Load(dictPath);
            }

            // Load Assemblies (either isolated or default)
            Assembly asmOfficial;
            Assembly asmEngine;

            if (alc != null)
            {
                asmOfficial = alc.LoadFromAssemblyPath(Path.Combine(InstallationPath, "PathosOfficial.dll"));
                asmEngine = alc.LoadFromAssemblyPath(Path.Combine(InstallationPath, "PathosEngine.dll"));
            }
            else
            {
                asmOfficial = Assembly.LoadFrom(Path.Combine(InstallationPath, "PathosOfficial.dll"));
                asmEngine = Assembly.LoadFrom(Path.Combine(InstallationPath, "PathosEngine.dll"));
            }

            var v = asmEngine.GetName().Version;
            GameVersion = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "未知版本";

            var campaignType = asmOfficial.GetType("Pathos.OfficialCampaign")!;
            AdventureType = asmEngine.GetType("Pathos.Adventure")!;
            EngineType = asmEngine.GetType("Pathos.Engine")!;
            SeedType = asmEngine.GetType("Pathos.Seed")!;

            seedFromTextMethod = SeedType.GetMethod("FromText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
            seedFromCodeMethod = SeedType.GetMethod("FromCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;

            var randomSupportType = asmEngine.GetType("Pathos.RandomSupport")!;
            reseedMethod = randomSupportType.GetMethod("Reseed", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(int) }, null)!;
            prepareShuffleMethod = AdventureType.GetMethod("PrepareShuffle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            commitShuffleMethod = AdventureType.GetMethod("CommitShuffle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            reverseShuffleMethod = AdventureType.GetMethod("ReverseShuffle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;

            // Load Codex
            string codexPath = Path.Combine(InstallationPath, "Assets", "Pathos.Codex");
            if (!File.Exists(codexPath))
            {
                throw new FileNotFoundException($"未能找到 Pathos.Codex: {codexPath}");
            }

            using var codexStream = File.OpenRead(codexPath);
            Campaign = Activator.CreateInstance(campaignType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { codexStream }, null)!;
            Manifest = Campaign.Manifest;

            foreach (var m in (IEnumerable)Campaign.Modules)
            {
                dynamic dm = m;
                if ((string)dm.Handle == "nethack codex")
                {
                    NethackModule = dm;
                    break;
                }
            }

            if (NethackModule == null)
            {
                throw new InvalidOperationException("在 Campaign 中未找到 'nethack codex' 模组！");
            }

            IsReady = true;
        }

        public dynamic CreateSeed(string seedText)
        {
            return seedFromTextMethod.Invoke(null, new object[] { seedText })!;
        }

        public dynamic CreateSeedFromCode(int code)
        {
            return seedFromCodeMethod.Invoke(null, new object[] { code })!;
        }

        public void Reseed(int code)
        {
            reseedMethod.Invoke(null, new object[] { code });
        }

        public void PrepareShuffle(dynamic adventure)
        {
            prepareShuffleMethod.Invoke(adventure, null);
        }

        public void CommitShuffle(dynamic adventure)
        {
            commitShuffleMethod.Invoke(adventure, null);
        }

        public void ReverseShuffle(dynamic adventure)
        {
            reverseShuffleMethod.Invoke(adventure, null);
        }

        public dynamic CreateEngine(dynamic seed, bool masterMode = false, bool abolition = false)
        {
            var ctors = EngineType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var ctor = ctors.FirstOrDefault(c =>
            {
                var p = c.GetParameters();
                return p.Length > 0 && p[0].ParameterType.IsInstanceOfType(Manifest);
            }) ?? ctors.FirstOrDefault();

            object[] args;
            if (ctor != null)
            {
                var p = ctor.GetParameters();
                args = new object[p.Length];
                if (p.Length > 0) args[0] = Manifest;
            }
            else
            {
                args = new object[] { Manifest };
            }

            dynamic engine = Activator.CreateInstance(EngineType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;
            dynamic adventure = engine.Adventure;
            adventure.SetMasterMode(masterMode);
            adventure.SetAbolition(abolition);
            adventure.SetSeed(seed);
            adventure.SetModule(NethackModule.Handle);
            return engine;
        }
    }
}
