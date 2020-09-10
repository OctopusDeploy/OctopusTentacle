#nullable enable
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
        public void StringValueGetsReturnedCorrectly()
        {
            var value = "TestValue";

            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.Get<string?>("Test");
            result.Should().Be(value, because: "strings should get passed straight back");
        }

        [Test]
        public void IntValueGetsReturnedCorrectly()
        {
            var value = "10";

            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.Get<int>("Test");
            result.Should().Be(10, because: "ints should get parsed");
        }

        [Test]
        public void StringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.Get<byte[]>("Test");
            bytes.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""), because: "non-intrinsic types should be handled as though they'd been JSON serialized");
        }

        [Test]
        public void EncryptedStringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.Get<byte[]>("Test", protectionLevel: ProtectionLevel.MachineKey);
            bytes.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""), because: "encrypted non-intrinsic types should be handled as though they'd been JSON serialized");
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