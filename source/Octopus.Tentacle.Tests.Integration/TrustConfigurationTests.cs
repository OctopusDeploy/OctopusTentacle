#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class TrustConfigurationTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testPolling: false)]
        public async Task ChangingTheTrustedThumbprintsForAListeningTentacleShouldNotRequireARestart(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using (var clientAndTentacle = await tentacleConfigurationTestCase
                             .CreateBuilder()
                             .WithRetryDuration(TimeSpan.FromSeconds(10))
                             .Build(CancellationToken))
            {
                using var newCertificate = new CertificateGenerator(new SystemLog()).GenerateNew($"cn={Guid.NewGuid()}");
                
                // Add a new trusted thumbprint
                var addTrustCommand = new TestExecuteShellScriptCommandBuilder()                    
                    .SetScriptBodyForCurrentOs(
$@"cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
.\Tentacle.exe configure --instance {clientAndTentacle.RunningTentacle.InstanceName} --trust {newCertificate.Thumbprint}",
$@"#!/bin/sh
cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
./Tentacle configure --instance {clientAndTentacle.RunningTentacle.InstanceName} --trust {newCertificate.Thumbprint}")
                    .Build();
                

                var result = await clientAndTentacle.TentacleClient.ExecuteScript(addTrustCommand, CancellationToken);
                result.LogExecuteScriptOutput(Logger);
                result.ScriptExecutionResult.ExitCode.Should().Be(0);
                result.ProcessOutput.Any(x => x.Text.Contains("Adding 1 trusted Octopus Servers")).Should().BeTrue("Adding 1 trusted Octopus Servers should be logged");

                // Ensure the new thumbprint is trusted
                var checkCommunicationCommand = new TestExecuteShellScriptCommandBuilder()                    
                    .SetScriptBody(new ScriptBuilder().Print("Success..."))
                    .Build();

                var tentacleClientUsingNewCertificate = BuildTentacleClientForNewCertificate(newCertificate, clientAndTentacle);

                result = await tentacleClientUsingNewCertificate.ExecuteScript(checkCommunicationCommand, CancellationToken);
                result.LogExecuteScriptOutput(Logger);
                result.ScriptExecutionResult.ExitCode.Should().Be(0);
                result.ProcessOutput.Any(x => x.Text.Contains("Success...")).Should().BeTrue("Success... should be logged");;

                // Remove trust for the old thumbprint
                var removeTrustCommand = new TestExecuteShellScriptCommandBuilder()                    
                    .SetScriptBodyForCurrentOs(
                        $@"cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
.\Tentacle.exe configure --instance {clientAndTentacle.RunningTentacle.InstanceName} --remove-trust {clientAndTentacle.Server.Thumbprint}",
                        $@"#!/bin/sh
cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
./Tentacle configure --instance {clientAndTentacle.RunningTentacle.InstanceName} --remove-trust {clientAndTentacle.Server.Thumbprint}")
                    .Build();

                result = await tentacleClientUsingNewCertificate.ExecuteScript(removeTrustCommand, CancellationToken);
                result.LogExecuteScriptOutput(Logger);
                result.ScriptExecutionResult.ExitCode.Should().Be(0);
                result.ProcessOutput.Any(x => x.Text.Contains("Removing 1 trusted Octopus Servers")).Should().BeTrue("Removing 1 trusted Octopus Servers should be logged");

                // Ensure the old thumbprint is no longer trusted
                await AssertionExtensions
                    .Should(async () => await clientAndTentacle.TentacleClient.ExecuteScript(checkCommunicationCommand, CancellationToken))
                    .ThrowAsync<HalibutClientException>();
            }
        }

        static TentacleClient BuildTentacleClientForNewCertificate(X509Certificate2 newCertificate, ClientAndTentacle clientAndTentacle)
        {
            var halibutRuntime = new HalibutRuntimeBuilder()
                .WithServerCertificate(newCertificate)
                .WithHalibutTimeoutsAndLimits(clientAndTentacle.Server.ServerHalibutRuntime.TimeoutsAndLimits)
                .WithLegacyContractSupport()
                .Build();

            TentacleClient.CacheServiceWasNotFoundResponseMessages(halibutRuntime);

            var retrySettings = new RpcRetrySettings(true, TimeSpan.FromSeconds(5));
            var clientOptions = new TentacleClientOptions(retrySettings);

            var tentacleClient = new TentacleClient(clientAndTentacle.ServiceEndPoint, halibutRuntime, new DefaultScriptObserverBackoffStrategy(), new NoTentacleClientObserver(), clientOptions);

            return tentacleClient;
        }
    }
}
