using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract class IntegrationTest
    {
        public static int TimeoutInMiliseconds = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        public CancellationTokenSource WatchDogCancellation;

        private static ConcurrentDictionary<string, string> runningTests = new ();

        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource(TimeoutInMiliseconds);
            CancellationToken = cancellationTokenSource.Token;
            // CancellationToken.Register(() =>
            // {
            //     Assert.Fail("The test timed out.");
            // });
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            Logger.Fatal("Test started");

            WatchDogCancellation = new CancellationTokenSource();

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(10), WatchDogCancellation.Token);
                
                while (!WatchDogCancellation.IsCancellationRequested)
                {
                    Logger.Fatal("This test has been running for too long!");
                    Logger.Fatal("Currently running tests: " + string.Join(" ", runningTests.Keys));
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            });
            runningTests[TestContext.CurrentContext.Test.Name + "-" + TestContext.CurrentContext.Test.FullName] = "";
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Fatal("Test Finished");
            runningTests.TryRemove(TestContext.CurrentContext.Test.Name + "-" + TestContext.CurrentContext.Test.FullName, out _);
            WatchDogCancellation.Cancel();
            try
            {
                cancellationTokenSource?.Token.IsCancellationRequested.Should().BeFalse("The test timed out.");
            }
            finally
            {
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
            }
        }
    }

    // public class IntegrationTestTimeout : TimeoutAttribute
    // {
    //     public IntegrationTestTimeout() : base(IntegrationTest.TimeoutInMiliseconds)
    //     {
    //     }
    // }
}
