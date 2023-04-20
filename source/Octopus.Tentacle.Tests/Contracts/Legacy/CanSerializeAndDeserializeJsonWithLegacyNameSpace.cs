using System;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Util;

namespace Octopus.Tentacle.Tests.Contracts.Legacy
{
    public class CanSerializeAndDeserializeJsonWithLegacyNameSpace
    {
        [Test]
        public void ShouldSerializeLegacyTypeToLegacyNameSpaceAndAssemblies()
        {
            var serializer = CreateJsonSerializer();

            var memoryStream = new MemoryStream();
            var laJson = serializer.ToJson(new HasAThing(new ScriptTicket("foo")));
            laJson.Should().Contain(
                "Octopus.Shared.Contracts.ScriptTicket, Octopus.Shared",
                because: "It should make reference to the old namespace and assembly for backwards compatability with old tentacle or clients of tentacle.");
        }
        
        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJsonThatReferencesOldAssemblies()
        {
            var json = @"{""theThing"":{""$type"":""Octopus.Shared.Contracts.ScriptTicket, Octopus.Shared"",""TaskId"":""foo""}}";
            
            var serializer = CreateJsonSerializer();

            
            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(ScriptTicket));
        }

        private static JsonSerializer CreateJsonSerializer()
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
            };

            MessageSerializerBuilderExtensionMethods.AddLegacyContractSupportToJsonSerializer(jsonSerializerSettings);
            var serializer = JsonSerializer.Create(jsonSerializerSettings);
            return serializer;
        }
    }

    public class HasAThing
    {
        public HasAThing(object theThing)
        {
            TheThing = theThing;
        }

        public object TheThing { get; }
    }
}