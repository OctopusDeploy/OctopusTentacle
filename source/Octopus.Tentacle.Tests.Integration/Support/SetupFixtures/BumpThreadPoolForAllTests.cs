using System;
using System.Threading;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class BumpThreadPoolForAllTests : ISetupFixture
    {
        public void OneTimeSetUp(ILogger logger)
        {
            logger.Information("Bumping thread pool");
            var minWorkerPoolThreads = 5000;
            var minCompletionPortThreads = 5000;
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
            ThreadPool.SetMaxThreads(Math.Max(minWorkerPoolThreads, maxWorkerThreads), Math.Max(minCompletionPortThreads, maxCompletionPortThreads));
            ThreadPool.SetMinThreads(minWorkerPoolThreads, minCompletionPortThreads);
        }

        public void OneTimeTearDown(ILogger logger)
        {
        }
    }
}