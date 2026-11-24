using System;
using System.IO;
using NSubstitute;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Support
{
    class TentacleConfigurationBuilder
    {
        string settings = "";

        public TentacleConfigurationBuilder WithCertificateThumbprint(string thumbprint)
        {
            settings += $"<set key=\"Tentacle.CertificateThumbprint\">{thumbprint}</set>";
            return this;
        }

        public TemporaryTentacleConfiguration Build()
        {
            var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            var directory = new TemporaryDirectory(fileSystem);
            var configFile = Path.Combine(directory.DirectoryPath, "Tentacle.config");
            File.WriteAllText(configFile, $"<?xml version=\"1.0\" encoding=\"utf-8\"?><octopus-settings>{settings}</octopus-settings>");
            var store = new XmlFileKeyValueStore(fileSystem, configFile, encryptor: TestMachineKeyEncryptor.Current);

            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, configFile, store, store, null));

            var configuration = new WritableTentacleConfiguration(
                selector,
                Substitute.For<IHomeConfiguration>(),
                new CertificateGenerator(new NullLog()),
                Substitute.For<IProxyConfiguration>(),
                Substitute.For<IPollingProxyConfiguration>(),
                Substitute.For<ISystemLog>());

            return new TemporaryTentacleConfiguration(directory, configuration, selector);
        }
    }

    class TemporaryTentacleConfiguration : IDisposable
    {
        readonly TemporaryDirectory directory;

        public TemporaryTentacleConfiguration(TemporaryDirectory directory, WritableTentacleConfiguration configuration, IApplicationInstanceSelector selector)
        {
            this.directory = directory;
            Configuration = configuration;
            Selector = selector;
        }

        public WritableTentacleConfiguration Configuration { get; }
        public IApplicationInstanceSelector Selector { get; }

        public void Dispose() => directory.Dispose();
    }
}
