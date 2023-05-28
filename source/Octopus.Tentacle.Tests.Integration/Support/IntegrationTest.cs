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

        [SetUp]
        public void SetUp()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            cancellationTokenSource = new CancellationTokenSource(TimeoutInMiliseconds);
            CancellationToken = cancellationTokenSource.Token;
            CancellationToken.Register(() =>
            {
                Assert.Fail("The test timed out.");
            });
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Information("Tearing down");
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
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
