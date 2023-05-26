using System;
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

        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource(TimeoutInMiliseconds);
            CancellationToken = cancellationTokenSource.Token;
            CancellationToken.Register(() =>
            {
                Assert.Fail("The test timed out.");
            });
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());

            WatchDogCancellation = new CancellationTokenSource();

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(10), WatchDogCancellation.Token);
                
                while (!WatchDogCancellation.IsCancellationRequested)
                {
                    Logger.Fatal("This test has been running for too long!");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            });
        }

        [TearDown]
        public void TearDown()
        {
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

    public class IntegrationTestTimeout : TimeoutAttribute
    {
        public IntegrationTestTimeout() : base(IntegrationTest.TimeoutInMiliseconds)
        {
        }
    }
}
