using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration
{
    public class WhenExecutingAScriptWithScriptServiceV1
    {
        public class OnAListeningTentacle
        {
            [Test]
            public async Task TheScriptShouldBeExecuted()
            {
                var cancellationToken = CancellationToken.None;

                await using var tentacle = await SetupAndRunListeningTentacle(cancellationToken);
                var tentacleClient = new TentacleClientBuilder()
                    .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                    .Build(cancellationToken);
                
                //new PollingTentacleBuilder().DoStuff();

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody("echo \"WellKnownLogMessage\"")
                    .WithTaskId(Guid.NewGuid().ToString())
                    .Build();

                var scriptTicket = tentacleClient.ScriptService.StartScript(startScriptCommand);

                var completed = false;
                ScriptStatusResponse? lastResponse = null;

                while (!completed)
                {
                    var request = new ScriptStatusRequest(scriptTicket, 0);
                    lastResponse = tentacleClient.ScriptService.GetStatus(request);

                    if (lastResponse.State == ProcessState.Complete)
                    {
                        completed = true;
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
                }

                lastResponse.Should().NotBeNull();
                lastResponse.ExitCode.Should().Be(0);
                lastResponse.State.Should().Be(ProcessState.Complete);
                lastResponse.Logs.Any(x => x.Text.Contains("WellKnownLogMessage")).Should().BeTrue();
            }

            private async Task<Support.Tentacle> SetupAndRunListeningTentacle(CancellationToken cancellationToken)
            {
                var tentacle = await new ListeningTentacleBuilder()
                    .Build(cancellationToken);

                return tentacle;
            }
        }

    }
}
