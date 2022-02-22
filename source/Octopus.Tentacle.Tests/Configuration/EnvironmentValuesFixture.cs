#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class EnvironmentValuesFixture
    {
        private MapsTentacleEnvironmentValuesToConfigItems mapper = null!;

        [SetUp]
        public void SetUp()
        {
            mapper = new MapsTentacleEnvironmentValuesToConfigItems(Substitute.For<ILogFileOnlyLogger>());
        }

        [Test]
        public void HomeDirectoryCanBeMapped()
        {
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", @"c:\MyHome" } });
            var result = mapper.GetConfigurationValue("Octopus.Home");
            result.Should().Be(@"c:\MyHome", "Values provided by Shared are mapped");
        }

        [Test]
        public void ValueCanBeMappedDirectly()
        {
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { MapsTentacleEnvironmentValuesToConfigItems.ProxyHost.Name, "Data Source=.;Initial Catalog=OctopusDeploy-master;Trusted_connection=true" } });
            var result = mapper.GetConfigurationValue(PollingProxyConfiguration.ProxyHostSettingName);
            result.Should().Be("Data Source=.;Initial Catalog=OctopusDeploy-master;Trusted_connection=true", "Raw SQL connection string gets passed directly through");
        }

        [Test]
        public void SupportedEnvironmentValueNames()
        {
            var names = MapsTentacleEnvironmentValuesToConfigItems.SupportedEnvironmentValues
                .Select(x =>
                {
                    if (x is SensitiveEnvironmentVariable)
                        return x.Name + " (sensitive)";
                    return x.Name;
                })
                .OrderBy(x => x);

            //This list contains the supported environment value names, which is a public contract.
            var expected = new[]
            {
                "TENTACLE_APPLICATION_DIRECTORY",
                "TENTACLE_CERTIFICATE",
                "TENTACLE_CERTIFICATE_THUMBPRINT",
                "TENTACLE_LISTEN_IP",
                "TENTACLE_NO_LISTEN",
                "TENTACLE_POLLING_CUSTOM_PROXY_HOST",
                "TENTACLE_POLLING_CUSTOM_PROXY_PASSWORD (sensitive)",
                "TENTACLE_POLLING_CUSTOM_PROXY_PORT",
                "TENTACLE_POLLING_CUSTOM_PROXY_USER",
                "TENTACLE_POLLING_USE_DEFAULT_PROXY",
                "TENTACLE_SERVICE_PORT",
                "TENTACLE_TRUSTED_SERVERS"
            };
            names.Should().BeEquivalentTo(expected, "the environment variable names are part of our public contract");
        }
    }
}