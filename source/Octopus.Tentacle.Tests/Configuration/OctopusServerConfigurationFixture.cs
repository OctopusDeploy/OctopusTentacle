using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Configuration
{
    public class OctopusServerConfigurationFixture
    {
        [TestCase(CommunicationStyle.None, 0)]
        [TestCase(CommunicationStyle.TentaclePassive, 1)]
        [TestCase(CommunicationStyle.TentacleActive, 2)]
        public void CommunicationStyleSerializesAsANumber(CommunicationStyle style, int expected)
        {
            const string comStyleProperty = "\"CommunicationStyle\": ";

            var settings = Octopus.Shared.Configuration.JsonSerialization.GetDefaultSerializerSettings();
            settings.Formatting = Formatting.Indented;

            var config = new OctopusServerConfiguration("ABC") {CommunicationStyle = style};
            var json = JsonConvert.SerializeObject(config, settings);
            var result = json.Split('\n')
                .First(l => l.Trim().StartsWith(comStyleProperty))
                .Substring(comStyleProperty.Length + 1)
                .Trim()
                .Trim(',');
            
            result.Should().Be(expected.ToString());
        }
    }
}