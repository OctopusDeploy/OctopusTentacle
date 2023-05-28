using System;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract class IntegrationTest
    {
        public static int TimeoutInMilliseconds = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource(TimeoutInMilliseconds);
            CancellationToken = cancellationTokenSource.Token;
            CancellationToken.Register(() =>
            {
                Assert.Fail("The test timed out.");
            });
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
        }

        [TearDown]
        public void TearDown()
        {
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
        public IntegrationTestTimeout() : base(IntegrationTest.TimeoutInMilliseconds)
        {
        }
    }
}
