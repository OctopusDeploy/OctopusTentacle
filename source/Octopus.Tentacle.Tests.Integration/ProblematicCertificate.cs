using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ProblematicCertificate: IntegrationTest
    {
        [Test]
        public async Task Something()
        {
            await Task.CompletedTask;
            var t = Octopus.Tentacle.Tests.Integration.Support.Certificates.Bad.Thumbprint;

            //new X509Certificate2(File.ReadAllBytes(Support.Certificates.BadPfxPath));
            Console.WriteLine(t);
            using var clientTentacle = await new ClientAndTentacleBuilder(TentacleType.Polling)
                .WithTentacleCertificate(Support.Certificates.BadCertificate)
                .Build(CancellationToken);

            var client = clientTentacle.LegacyTentacleClientBuilder().Build(CancellationToken);
            client.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("sd"), 0));
        }
    }
}