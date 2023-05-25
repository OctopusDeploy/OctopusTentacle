using System;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public abstract class IntegrationTest
    {
        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            CancellationToken = cancellationTokenSource.Token;
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
}
