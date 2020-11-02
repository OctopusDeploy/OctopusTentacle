#nullable enable
using System.Collections.Generic;
using System.Linq;
using Assent;
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
        MapsTentacleEnvironmentValuesToConfigItems mapper = null!;

        [SetUp]
        public void SetUp()
        {
            mapper = new MapsTentacleEnvironmentValuesToConfigItems(Substitute.For<ILogFileOnlyLogger>());
        }

        [Test]
        public void HomeDirectoryCanBeMapped()
        {
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", @"c:\MyHome" }});
            var result = mapper.GetConfigurationValue("Octopus.Home");
            result.Should().Be(@"c:\MyHome", because: "Values provided by Shared are mapped");
        }

        [Test]
        public void ValueCanBeMappedDirectly()
        {
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { MapsTentacleEnvironmentValuesToConfigItems.ProxyHost.Name, "Data Source=.;Initial Catalog=OctopusDeploy-master;Trusted_connection=true" }});
            var result = mapper.GetConfigurationValue(PollingProxyConfiguration.ProxyHostSettingName);
            result.Should().Be("Data Source=.;Initial Catalog=OctopusDeploy-master;Trusted_connection=true", because: "Raw SQL connection string gets passed directly through");
        }

        [Test]
        public void SupportedEnvironmentValueNames()
        {
            var text = @"===================================================================================
This list contains the supported environment value names, which is a public contract.
===================================================================================
" + string.Join("\n",
                MapsTentacleEnvironmentValuesToConfigItems.SupportedEnvironmentValues.Select(x =>
                {
                    if (x is SensitiveEnvironmentVariable)
                        return x.Name + " (sensitive)";
                    return x.Name;
                }).OrderBy(x => x));
            this.Assent(text);
        }
    }
}