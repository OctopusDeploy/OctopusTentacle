using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class InMemoryKeyValueStoreFixture
    {
        [Test]
        public void StringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.Get<byte[]>("Test");
            bytes.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""));
        }

        [Test]
        public void EncryptedStringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.Get<byte[]>("Test", protectionLevel: ProtectionLevel.MachineKey);
            bytes.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""));
        }

        static byte[] GenerateValue()
        {
            var key = new byte[16];
            using (var provider = new RNGCryptoServiceProvider())
                provider.GetBytes(key);
            return key;
        }
    }
}