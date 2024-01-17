using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Diagnostics;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Serilog.Formatting.Display;
using Serilog;
using static Octopus.Tentacle.Tests.Integration.Util.SerilogLoggerBuilder;
using System.Text;
using Octopus.Tentacle.Tests.Integration.Support.Logging;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionLogsToProvidedLogger : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task WhenExecutingAScript_ShouldLogToTheProvidedILog(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder()
                .Build(CancellationToken);

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(new ScriptBuilder().Print("hello"))
                .Build();

            var logger = new InMemoryLog();

            var response = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand,
                _ => {},
                _ => Task.CompletedTask,
                logger,
                CancellationToken).ConfigureAwait(false);

            response.State.Should().Be(ProcessState.Complete);
            response.ExitCode.Should().Be(0);

            logger.LogEvents.Should().HaveCount(1);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenExecutingAScript_ShouldLogToTheProvidedILogger(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder()
                .Build(CancellationToken);

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(new ScriptBuilder().Print("hello"))
                .Build();

            var inMemorySink = new InMemorySink();
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(inMemorySink)
                .CreateLogger();

            var response = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand,
                _ => {},
                _ => Task.CompletedTask,
                logger,
                CancellationToken).ConfigureAwait(false);

            response.State.Should().Be(ProcessState.Complete);
            response.ExitCode.Should().Be(0);

            inMemorySink.LogEvents.Should().HaveCount(1);
        }
    }
}
