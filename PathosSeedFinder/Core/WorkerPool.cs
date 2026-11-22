using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PathosSeedFinder.Core
{
    /// <summary>
    /// 管理独立隔离的 PathosContext 工作实例池，支持全核心无锁并发搜索。
    /// 参考 SHPD 搜种器的 WorkerPool 模式，利用独立 AssemblyLoadContext 隔离静态状态。
    /// </summary>
    public static class WorkerPool
    {
        public static int MaxCores => Math.Max(1, Environment.ProcessorCount);
        public static int DefaultCores => Math.Clamp(Environment.ProcessorCount > 2 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount, 1, 16);

        private static readonly ConcurrentBag<PathosContext> _availableWorkers = new();
        private static readonly SemaphoreSlim _poolSemaphore = new(MaxCores, MaxCores);
        private static int _createdCount = 0;

        /// <summary>
        /// 从池中借出一个独立的 PathosContext。若池为空且未达最大限制，则快速并行创建新实例。
        /// </summary>
        public static async Task<PathosContext> RentAsync(string pathosDir, CancellationToken cancellationToken = default)
        {
            await _poolSemaphore.WaitAsync(cancellationToken);

            if (_availableWorkers.TryTake(out var existing))
            {
                return existing;
            }

            // 无锁并发创建独立的 ALC 镜像实例
            var created = PathosContext.CreateIsolated(pathosDir);
            Interlocked.Increment(ref _createdCount);
            return created;
        }

        /// <summary>
        /// 将用完的工作上下文归还池中，供后续并发任务复用。
        /// </summary>
        public static void Return(PathosContext ctx)
        {
            if (ctx != null)
            {
                _availableWorkers.Add(ctx);
                _poolSemaphore.Release();
            }
        }

        /// <summary>
        /// 预热指定数量的 Worker 上下文并填充至池中，消除首次并发的冷启动耗时。
        /// </summary>
        public static void Prewarm(string pathosDir, int count)
        {
            int target = Math.Clamp(count, 1, MaxCores);
            Task.Run(() =>
            {
                try
                {
                    Parallel.For(0, target, new ParallelOptions { MaxDegreeOfParallelism = 4 }, _ =>
                    {
                        if (Interlocked.Increment(ref _createdCount) <= target)
                        {
                            var ctx = PathosContext.CreateIsolated(pathosDir);
                            try
                            {
                                SeedAnalyzer.Analyze("warmup", ctx);
                            }
                            catch { }
                            _availableWorkers.Add(ctx);
                        }
                    });
                }
                catch { }
            });
        }
    }
}
