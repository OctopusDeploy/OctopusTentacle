#if NET
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Common;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Integration.Commands
{
    [LinuxTest]
    [UnsupportedOSPlatform("windows")]
    public class LinuxResetConfigurationEncryptionTests : IntegrationTest
    {
        [Test]
        public async Task WhenTheKeyIsLost_AndResetConfigurationEncryptionIsCalled_ThenProtectedSettingsAreRemovedAndANewKeyIsCreated()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "polling-proxy", CancellationToken,
                "--proxyEnable=true", "--proxyHost=proxy.example", "--proxyPort=8080", "--proxyUsername=user", "--proxyPassword=secret");
            File.Delete(instance.ConfigurationEncryptionKeyFile);

            // Act
            var reset = await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "reset-configuration-encryption", CancellationToken);

            // Assert
            File.Exists(instance.ConfigurationEncryptionKeyFile).Should().BeTrue();
            var settings = XDocument.Load(instance.ConfigurationFile).Root!.Elements("set")
                .ToDictionary(e => (string)e.Attribute("key")!, e => e.Value);
            settings.Should().NotContainKey(TentacleConfiguration.CertificateSettingName);
            settings.Should().NotContainKey(PollingProxyConfiguration.ProxyPasswordSettingName);
            settings[TentacleConfiguration.CertificateThumbprintSettingName].Should().Be(instance.Thumbprint);
            reset.StandardOutput.Should().Contain(instance.Thumbprint);
        }

        [Test]
        public async Task WhenTheKeyIsLost_AndTheTentacleIsResetAndGivenANewCertificate_ThenOctopusCanRunScriptsOnItAgain()
        {
            // Arrange
            await using var server = await new FakeOctopusServerBuilder().Build();
            var octopusUrl = $"http://localhost:{server.Port}";
            await using var runningTentacle = await new TentacleInstanceBuilder()
                .RunAsListeningTentacle(TestCertificates.ServerPublicThumbprint)
                .Build(Logger, CancellationToken);
            var instance = runningTentacle.Instance;
            await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "register-with", CancellationToken,
                $"--server={octopusUrl}", "--apiKey=API-NOTAREALKEY", $"--space={FakeOctopusServerBuilder.SpaceName}",
                $"--environment={FakeOctopusServerBuilder.EnvironmentName}", "--role=web", "--name=recovered");
            await runningTentacle.Stop(CancellationToken);
            File.Delete(instance.ConfigurationEncryptionKeyFile);

            // Act
            await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "reset-configuration-encryption", CancellationToken);
            await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "replace-certificate", CancellationToken,
                $"--server={octopusUrl}", "--apiKey=API-NOTAREALKEY");
            await runningTentacle.Start(CancellationToken);

            // Assert
            var newThumbprint = await instance.ShowThumbprint(CancellationToken);
            newThumbprint.Should().NotBe(runningTentacle.Thumbprint);
            server.LatestRegistration(FakeOctopusServerBuilder.SpaceName, "recovered")!.Thumbprint.Should().Be(newThumbprint);
            var tentacleClient = TestServerHalibut.BuildTentacleClient(runningTentacle.ServiceUri, newThumbprint, server.Halibut, _ => { });
            var (scriptResult, _) = await tentacleClient.ExecuteScript(new TestExecuteShellScriptCommandBuilder().SetScriptBody(b => b.Print("Hello")).Build(), CancellationToken);
            scriptResult.State.Should().Be(ProcessState.Complete);
            scriptResult.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndResetConfigurationEncryptionIsCalled_ThenItRefusesAndTheCertificateIsUnchanged()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = instance.CertificateAsStoredInTheConfigurationFile();

            // Act
            var reset = await instance.WithCurrentTentacleExe().ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "reset-configuration-encryption", CancellationToken);

            // Assert
            var output = reset.StandardOutput + reset.StandardError;
            reset.ExitCode.Should().NotBe(0, output);
            output.Should().Contain("upgrade-configuration-encryption");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedByVersion14);
        }
    }
}
#endif
