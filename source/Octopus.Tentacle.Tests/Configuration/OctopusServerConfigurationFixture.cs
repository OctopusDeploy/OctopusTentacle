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

            var settings = GetSettings();
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

        static JsonSerializerSettings GetSettings()
            => Octopus.Shared.Configuration.JsonSerialization.GetDefaultSerializerSettings();

        [Test]
        public void CommunicationStyleRoundtripsCorrectly()
        {
            var settings = GetSettings();
            var config = new OctopusServerConfiguration("ABC") {CommunicationStyle = CommunicationStyle.TentacleActive};
            var json = JsonConvert.SerializeObject(config, settings);
            var result = JsonConvert.DeserializeObject<OctopusServerConfiguration>(json, settings);
            result.CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
        }
        
        
        [Test]
        public void CommunicationStyleAsStringCanBeRead()
        {
            var settings = GetSettings();
            var result = JsonConvert.DeserializeObject<OctopusServerConfiguration>(@"{""CommunicationStyle"": ""TentacleActive"", ""Thumbprint"": ""A""}", settings);
            result.CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
        }
        
        [Test]
        public void CommunicationStyleAsIntCanBeRead()
        {
            var settings = GetSettings();
            var result = JsonConvert.DeserializeObject<OctopusServerConfiguration>(@"{""CommunicationStyle"": 2, ""Thumbprint"": ""A"" }", settings);
            result.CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
        }
    }
}