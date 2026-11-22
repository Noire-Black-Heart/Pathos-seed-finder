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
        private static bool resolverRegistered = false;
        private static string currentResolveDir = "";

        public static void RegisterAssemblyResolver(string gameDir)
        {
            currentResolveDir = gameDir;
            if (resolverRegistered) return;
            resolverRegistered = true;

            AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
            {
                if (!string.IsNullOrEmpty(currentResolveDir))
                {
                    string candidate = Path.Combine(currentResolveDir, assemblyName.Name + ".dll");
                    if (File.Exists(candidate))
                    {
                        return context.LoadFromAssemblyPath(candidate);
                    }
                }
                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(currentResolveDir))
                {
                    var name = new AssemblyName(args.Name).Name;
                    string candidate = Path.Combine(currentResolveDir, name + ".dll");
                    if (File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                }
                return null;
            };
        }

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
                    RegisterAssemblyResolver(path);
                    var alc = new IsolatedAssemblyLoadContext(path);
                    var ctx = new PathosContext(path, alc);
                    instance = ctx;
                    var settings = SettingsManager.Load();
                    settings.PathosDirectory = path;
                    SettingsManager.Save(settings);
                    error = "";
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CRITICAL MOUNT EXCEPTION] {ex}");
                    error = $"挂载游戏目录失败: {ex.Message}";
                    return false;
                }
            }
        }

        public static PathosContext CreateIsolated(string path)
        {
            RegisterAssemblyResolver(path);
            var alc = new IsolatedAssemblyLoadContext(path);
            return new PathosContext(path, alc);
        }

        private static Type SafeGetType(Assembly asm, string typeName)
        {
            try
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                var msgs = string.Join("; ", rtle.LoaderExceptions.Where(e => e != null).Select(e => e!.Message));
                throw new InvalidOperationException($"程序集 {asm.GetName().Name} 中无法加载类型 {typeName}。依赖加载错误: {msgs}", rtle);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"程序集 {asm.GetName().Name} 中查找类型 {typeName} 失败: {ex.Message}", ex);
            }

            throw new InvalidOperationException($"在程序集 {asm.GetName().Name} 中未找到类型 '{typeName}'，请检查游戏文件完整性或版本兼容性。");
        }

        private static MethodInfo SafeGetMethod(Type type, string methodName, BindingFlags flags, Type[]? parameterTypes = null)
        {
            MethodInfo? m = parameterTypes != null
                ? type.GetMethod(methodName, flags, null, parameterTypes, null)
                : type.GetMethod(methodName, flags);

            if (m == null)
            {
                throw new InvalidOperationException($"在类型 '{type.FullName}' 中未能找到所需的方法 '{methodName}'。");
            }
            return m;
        }

        public PathosContext(string path, AssemblyLoadContext? alc = null)
        {
            InstallationPath = path;
            RegisterAssemblyResolver(path);

            alc ??= new IsolatedAssemblyLoadContext(path);

            // Load translation dictionary
            string dictPath = Path.Combine(InstallationPath, "Assets", "zh-Simplified.Dictionary");
            if (File.Exists(dictPath))
            {
                Translations.Load(dictPath);
            }

            // Load Assemblies via Isolated ALC
            Assembly asmOfficial;
            Assembly asmEngine;

            try
            {
                asmOfficial = alc.LoadFromAssemblyPath(Path.Combine(InstallationPath, "PathosOfficial.dll"));
                asmEngine = alc.LoadFromAssemblyPath(Path.Combine(InstallationPath, "PathosEngine.dll"));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载游戏程序集失败: {ex.Message}", ex);
            }

            var v = asmEngine.GetName().Version;
            GameVersion = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "未知版本";

            var campaignType = SafeGetType(asmOfficial, "Pathos.OfficialCampaign");
            AdventureType = SafeGetType(asmEngine, "Pathos.Adventure");
            EngineType = SafeGetType(asmEngine, "Pathos.Engine");
            SeedType = SafeGetType(asmEngine, "Pathos.Seed");

            seedFromTextMethod = SafeGetMethod(SeedType, "FromText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            seedFromCodeMethod = SafeGetMethod(SeedType, "FromCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var randomSupportType = SafeGetType(asmEngine, "Pathos.RandomSupport");
            reseedMethod = SafeGetMethod(randomSupportType, "Reseed", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, new[] { typeof(int) });
            prepareShuffleMethod = SafeGetMethod(AdventureType, "PrepareShuffle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            commitShuffleMethod = SafeGetMethod(AdventureType, "CommitShuffle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            reverseShuffleMethod = SafeGetMethod(AdventureType, "ReverseShuffle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Load Codex
            string codexPath = Path.Combine(InstallationPath, "Assets", "Pathos.Codex");
            if (!File.Exists(codexPath))
            {
                throw new FileNotFoundException($"未能找到 Pathos.Codex: {codexPath}");
            }

            using var codexStream = File.OpenRead(codexPath);
            Campaign = Activator.CreateInstance(campaignType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { codexStream }, null)
                ?? throw new InvalidOperationException("无法初始化 Pathos 官方战役对象 (OfficialCampaign)！");

            try
            {
                Manifest = Campaign.Manifest;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"读取战役清单 (Campaign.Manifest) 失败: {ex.Message}", ex);
            }

            if (Manifest == null)
            {
                throw new InvalidOperationException("游戏战役清单 (Campaign.Manifest) 为空！");
            }

            if (Campaign.Modules == null)
            {
                throw new InvalidOperationException("游戏模组列表 (Campaign.Modules) 为空！");
            }

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
