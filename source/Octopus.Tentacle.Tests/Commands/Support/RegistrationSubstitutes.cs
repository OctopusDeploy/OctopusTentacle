using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Repositories.Async;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Tests.Commands.Support
{
    /// <summary>
    /// The substitutes a register command needs, wired to an Octopus Server reporting the given version.
    /// </summary>
    public class RegistrationSubstitutes<TOperation> where TOperation : class
    {
        public RegistrationSubstitutes(string serverVersion)
        {
            ServerThumbprint = Guid.NewGuid().ToString();
            Configuration = Substitute.For<IWritableTentacleConfiguration>();
            Operation = Substitute.For<TOperation>();
            ServerChecker = Substitute.For<IOctopusServerChecker>();
            Log = Substitute.For<ISystemLog>();
            ClientInitializer = Substitute.For<IOctopusClientInitializer>();
            var octopusAsyncClient = Substitute.For<IOctopusAsyncClient>();

            Repository = Substitute.For<IOctopusAsyncRepository>();
            Repository.Client.Returns(octopusAsyncClient);
            Repository.LoadRootDocument(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RootResource { Version = serverVersion }));
            octopusAsyncClient.Repository.Returns(Repository);
            octopusAsyncClient.ForSystem().Returns(Repository);
            octopusAsyncClient.ForSpace(Arg.Any<SpaceResource>()).Returns(Repository);

            var certificateConfigurationRepository = Substitute.For<ICertificateConfigurationRepository>();
            var certificateConfigurationResource = new CertificateConfigurationResource { Thumbprint = ServerThumbprint };
            certificateConfigurationRepository.GetOctopusCertificate().Returns(Task.FromResult(certificateConfigurationResource));
            Repository.CertificateConfiguration.Returns(certificateConfigurationRepository);
            ClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), false)
                .Returns(Task.FromResult(octopusAsyncClient));

            Configuration.ServicesPortNumber.Returns(90210);
            Certificate = new CertificateGenerator(new NullLog()).GenerateNew("CN=Hello");
            Configuration.TentacleCertificate.Returns(Certificate);
        }

        public string ServerThumbprint { get; }
        public IWritableTentacleConfiguration Configuration { get; }
        public TOperation Operation { get; }
        public IOctopusServerChecker ServerChecker { get; }
        public ISystemLog Log { get; }
        public IOctopusClientInitializer ClientInitializer { get; }
        public IOctopusAsyncRepository Repository { get; }
        public X509Certificate2 Certificate { get; }
    }
}
