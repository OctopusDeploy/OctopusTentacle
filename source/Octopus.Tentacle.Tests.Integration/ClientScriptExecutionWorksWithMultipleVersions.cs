using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutionWorksWithMultipleVersions
    {
        [TestCase(true, null)] // The version of tentacle compiled from the current code.
        [TestCase(false, "5.0.4")] // First linux Release 9/9/2019
        [TestCase(false, "5.0.12")] // The autofac service was in octopus shared.
        [TestCase(false, "6.3.451")] // the autofac service is in tentacle, but tentacle does not have the capabilities service.
        public async Task CanRunScript(bool useTentacleBuiltFromCurrentCode, string version)
        {
            var token = TestCancellationToken.Token();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using var tmp = new TemporaryDirectory();
            var tentacleExe = useTentacleBuiltFromCurrentCode ? TentacleExeFinder.FindTentacleExe() : await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, version);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .WithTentacleExe(tentacleExe)
                       .Build(CancellationToken.None))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var tentacleClient = new Client.TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), null, TimeSpan.FromSeconds(1000));

                // TODO this could be more realistic. e.g. log lots of stuff over time.
                var bashScript = "echo hello\nsleep 10";
                var windowsScript = "echo hello\r\nStart-Sleep -Seconds 10";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .Build();

                List<ProcessOutput> logs = new List<ProcessOutput>();
                var finalResponse = await tentacleClient.ExecuteScript(startScriptCommand, onScriptStatusResponseReceived =>
                    {
                        logs.AddRange(onScriptStatusResponseReceived.Logs);
                    }, cts =>
                    {
                        return Task.CompletedTask;
                    },
                    new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                    token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);

                // TODO this should tests order of logs.
                allLogs.Should().Contain("hello");
            }
        }

        private static string JoinLogs(List<ProcessOutput> logs)
        {
            return String.Join(" ", logs.Select(l => l.Text).ToArray());
        }
    }
}