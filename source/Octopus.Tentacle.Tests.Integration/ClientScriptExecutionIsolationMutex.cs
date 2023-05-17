using System;
using System.IO;
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
    public class ClientScriptExecutionIsolationMutex
    {
        
        [TestCase(true, null, ScriptIsolationLevel.FullIsolation)] // Has Script service v2
        [TestCase(false, "6.3.451", ScriptIsolationLevel.FullIsolation)] // Script Service v1
        [TestCase(true, null, ScriptIsolationLevel.NoIsolation)] // Has Script service v2
        [TestCase(false, "6.3.451", ScriptIsolationLevel.NoIsolation)] // Script Service v1
        public async Task ScriptIsolationMutexFull_EnsuresTwoDifferentScriptsDontRunAtTheSameTime(bool useTentacleBuiltFromCurrentCode, string version, ScriptIsolationLevel levelOfSecondScript)
        {
            //TODO
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
                var firstScriptStartFile = Path.Combine(tmp.DirectoryPath, "firstScriptStartFile");
                var firstScriptWaitFile = Path.Combine(tmp.DirectoryPath, "firstScriptWaitFile");
                
                var secondScriptStart = Path.Combine(tmp.DirectoryPath, "secondScriptStartFile");


                var firstStartScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .CreateFile(firstScriptStartFile)
                        .WaitForFileToExist(firstScriptWaitFile))
                    .WithIsolation(ScriptIsolationLevel.FullIsolation)
                    .WithMutexName("mymutex")
                    .Build();
                
                
                var secondStartScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder().CreateFile(secondScriptStart))
                    .WithIsolation(levelOfSecondScript)
                    .WithMutexName("mymutex")
                    .Build();

                CountingCallsScriptServiceV2Decorator? callCounts = null;
                CountingCallsScriptServiceDecorator? callCountsV1 = null;
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(inner => callCounts = new CountingCallsScriptServiceV2Decorator(inner))
                    .DecorateScriptServiceWith(inner => callCountsV1 = new CountingCallsScriptServiceDecorator(inner))
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));
                var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstStartScriptCommand, token));

                // Wait for the first script to start running
                await Wait.For(() => File.Exists(firstScriptStartFile), token);
                
                var secondScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(secondStartScriptCommand, token));

                // Wait for the second script start script RPC call to return. 
                await Wait.For(() => (callCounts.StartScriptCallCountComplete + callCountsV1.StartScriptCallCountComplete) == 2, token);

                // Give Tentacle some more time to run the script (although it should not).
                await Task.Delay(TimeSpan.FromSeconds(2));

                File.Exists(secondScriptStart).Should().BeFalse("The second script must not be started while the first is running with a FullIsolationMutex");
                
                // Let the first script finish.
                File.WriteAllText(firstScriptWaitFile, "");

                var(finalResponseFirstScript, _) = await firstScriptExecution;
                var(finalResponseSecondScript, _) = await secondScriptExecution;
                
                File.Exists(secondScriptStart).Should().BeTrue("The second should now have run.");

                finalResponseFirstScript.ExitCode.Should().Be(0);
                finalResponseSecondScript.ExitCode.Should().Be(0);
            }
        }
        
        [TestCase(true, null,ScriptIsolationLevel.FullIsolation, "mutex", ScriptIsolationLevel.FullIsolation, "differentMutex")]
        [TestCase(true, null,ScriptIsolationLevel.NoIsolation, "sameMutex", ScriptIsolationLevel.NoIsolation, "sameMutex")]
        [TestCase(false, "6.3.451", ScriptIsolationLevel.FullIsolation, "mutex", ScriptIsolationLevel.FullIsolation, "differentMutex")]
        [TestCase(false, "6.3.451", ScriptIsolationLevel.NoIsolation, "sameMutex", ScriptIsolationLevel.NoIsolation, "sameMutex")]
        public async Task ScriptIsolationMutexFull_IsOnlyExclusiveWhenFullAndWhenTheMutexNameIsTheSame(
            bool useTentacleBuiltFromCurrentCode,
            string version,
            ScriptIsolationLevel levelOfFirstScript, 
            string mutexForFirstScript,
            ScriptIsolationLevel levelOfSecondScript,
            string mutexForSecondScript)
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
                var firstScriptStartFile = Path.Combine(tmp.DirectoryPath, "firstScriptStartFile");
                var firstScriptWaitFile = Path.Combine(tmp.DirectoryPath, "firstScriptWaitFile");
                
                var secondScriptStart = Path.Combine(tmp.DirectoryPath, "secondScriptStartFile");


                var firstStartScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .CreateFile(firstScriptStartFile)
                        .WaitForFileToExist(firstScriptWaitFile))
                    .WithIsolation(levelOfFirstScript)
                    .WithMutexName(mutexForFirstScript)
                    .Build();
                
                
                var secondStartScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder().CreateFile(secondScriptStart))
                    .WithIsolation(levelOfSecondScript)
                    .WithMutexName(mutexForSecondScript)
                    .Build();
                
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));
                var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstStartScriptCommand, token));

                // Wait for the first script to start running
                await Wait.For(() => File.Exists(firstScriptStartFile), token);
                
                var secondScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(secondStartScriptCommand, token));
                
                // Wait for the second script to start
                await Wait.For(() => File.Exists(secondScriptStart), token);
                // Both scripts are now running in parallel

                // Let the first script finish.
                File.WriteAllText(firstScriptWaitFile, "");

                var(finalResponseFirstScript, _) = await firstScriptExecution;
                var(finalResponseSecondScript, _) = await secondScriptExecution;
                
                File.Exists(secondScriptStart).Should().BeTrue("The second script must not be started while the first is running with a FullIsolationMutex");

                finalResponseFirstScript.ExitCode.Should().Be(0);
                finalResponseSecondScript.ExitCode.Should().Be(0);
            }
        }
    }
}