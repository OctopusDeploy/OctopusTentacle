#if PIPELINES_RUNNER
#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Pipelines;

namespace Octopus.Tentacle.Tests.Pipelines
{
    [TestFixture]
    public class SystemLogLoggerProviderFixture
    {
        [TestCase(LogLevel.Trace, "Trace")]
        [TestCase(LogLevel.Debug, "Verbose")]
        [TestCase(LogLevel.Information, "Info")]
        [TestCase(LogLevel.Warning, "Warn")]
        [TestCase(LogLevel.Error, "Error")]
        [TestCase(LogLevel.Critical, "Error")]
        public void MapsLevels(LogLevel level, string method)
        {
            var log = Substitute.For<ISystemLog>();
            using var provider = new SystemLogLoggerProvider(log);
            var logger = provider.CreateLogger("Test");

            logger.Log(level, "hi");

            var call = log.ReceivedCalls().Should().ContainSingle().Subject;
            call.GetMethodInfo().Name.Should().Be(method);
            call.GetArguments().Should().Equal("[Pipelines runner] hi");
        }

        [Test]
        public void PassesExceptionsThrough()
        {
            var log = Substitute.For<ISystemLog>();
            using var provider = new SystemLogLoggerProvider(log);
            var error = new InvalidOperationException("boom");

            provider.CreateLogger("Test").LogWarning(error, "failed {Thing}", "x");

            log.Received(1).Warn(error, "[Pipelines runner] failed x");
        }

        [Test]
        public void IsEnabledForEverythingButNone()
        {
            using var provider = new SystemLogLoggerProvider(Substitute.For<ISystemLog>());
            var logger = provider.CreateLogger("Test");

            Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().Where(l => l != LogLevel.None).Should().OnlyContain(l => logger.IsEnabled(l));
            logger.IsEnabled(LogLevel.None).Should().BeFalse();
        }
    }
}
#endif
