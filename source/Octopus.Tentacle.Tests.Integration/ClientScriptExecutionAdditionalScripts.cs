using System;
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
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutionAdditionalScripts
    {
        [TestCase(true, null)] // Has Script service v2
        [TestCase(false, "6.3.451")] // Script Service v1
        public async Task AdditionalScriptsWork(bool useTentacleBuiltFromCurrentCode, string version)
        {
            var token = TestCancellationToken.Token();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);
            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = useTentacleBuiltFromCurrentCode ? TentacleExeFinder.FindTentacleExe() : await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, version);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .WithTentacleExe(oldTentacleExe)
                       .Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var scriptBuilder = new ScriptBuilder().Print("Hello");
                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithAdditionalScriptTypes(ScriptType.Bash, scriptBuilder.BuildBashScript())
                    // Additional Scripts don't actually work on tentacle for anything other than bash.
                    // Below is what we would have expected to tentacle to work with.
                    //.WithAdditionalScriptTypes(ScriptType.PowerShell, scriptBuilder.BuildPowershellScript())
                    // But instead we need to send the powershell in the scriptbody.
                    .WithScriptBody(scriptBuilder.BuildPowershellScript())
                    .Build();
                
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));
                var (finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);
                
                
                var allLogs = logs.JoinLogs();

                allLogs.Should().Contain("Hello");
            }
        }
    }
}