#if PIPELINES_RUNNER
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Pipelines;

namespace Octopus.Tentacle.Tests.Pipelines
{
    [TestFixture]
    public class TentacleRunnerSettingsFixture
    {
        static OctopusServerConfiguration Listening(string thumbprint)
            => new OctopusServerConfiguration(thumbprint)
            {
                CommunicationStyle = CommunicationStyle.TentaclePassive,
                Address = new Uri($"https://listening-{thumbprint}:10933")
            };

        static OctopusServerConfiguration Polling(string thumbprint, string? address)
            => new OctopusServerConfiguration(thumbprint)
            {
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = address == null ? null! : new Uri(address)
            };

        static X509Certificate2 NewCertificate()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }

        static ITentacleConfiguration Configuration(
            OctopusServerConfiguration[] servers,
            X509Certificate2? certificate = null,
            string? squid = "SQ-ABC",
            bool noCertificate = false)
        {
            var configuration = Substitute.For<ITentacleConfiguration>();
            configuration.TrustedOctopusServers.Returns(servers);
            configuration.TentacleCertificate.Returns(noCertificate ? null : certificate ?? NewCertificate());
#pragma warning disable CS0618
            configuration.TentacleSquid.Returns(squid);
#pragma warning restore CS0618
            return configuration;
        }

        [Test]
        public void UsesTheFirstPollingServer()
        {
            var configuration = Configuration(new[] { Listening("A"), Polling("B", "https://b:10943"), Polling("C", "https://c:10943") });

            var settings = TentacleRunnerSettings.From(configuration, false, false, out var skipReason);

            skipReason.Should().BeNull();
            settings.Should().NotBeNull();
            settings!.ServerAddress.Should().Be(new Uri("https://b:10943"));
            settings.ServerThumbprint.Should().Be("B");
        }

        [Test]
        public void SkipsWhenThereIsNoPollingServer()
        {
            var settings = TentacleRunnerSettings.From(Configuration(new[] { Listening("A") }), false, false, out var skipReason);

            settings.Should().BeNull();
            skipReason.Should().Contain("polling");
        }

        [Test]
        public void SkipsWhenThePollingServerHasNoAddress()
        {
            var settings = TentacleRunnerSettings.From(Configuration(new[] { Polling("B", null) }), false, false, out var skipReason);

            settings.Should().BeNull();
            skipReason.Should().Contain("address");
        }

        [Test]
        public void SkipsWhenTentacleHasNoCertificate()
        {
            var configuration = Configuration(new[] { Polling("B", "https://b:10943") }, noCertificate: true);

            var settings = TentacleRunnerSettings.From(configuration, false, false, out var skipReason);

            settings.Should().BeNull();
            skipReason.Should().Contain("certificate");
        }

        [Test]
        public void SkipsOnTheKubernetesAgent()
        {
            var settings = TentacleRunnerSettings.From(Configuration(new[] { Polling("B", "https://b:10943") }), true, false, out var skipReason);

            settings.Should().BeNull();
            skipReason.Should().Contain("Kubernetes agent");
        }

        [Test]
        public void SkipsOnWindows()
        {
            var settings = TentacleRunnerSettings.From(Configuration(new[] { Polling("B", "https://b:10943") }), false, true, out var skipReason);

            settings.Should().BeNull();
            skipReason.Should().Contain("Windows");
        }

        [Test]
        public void RunnerIdIsTheTentacleSquid()
        {
            var servers = new[] { Polling("B", "https://b:10943") };

            TentacleRunnerSettings.From(Configuration(servers, squid: "SQ-ABC"), false, false, out _)!.RunnerId.Should().Be("SQ-ABC");
            TentacleRunnerSettings.From(Configuration(servers, squid: null), false, false, out _)!.RunnerId.Should().Be(Environment.MachineName);
        }
    }
}
#endif
