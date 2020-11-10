#nullable enable
using System;
using System.Security.Cryptography;
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
        public void NullStringValueGetsReturnedCorrectly()
        {
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns((string?)null);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.TryGet<string?>("Test");
            result.foundResult.Should().BeFalse(because: "defaulted value should tell you it wasn't found, and is thus the default");
            result.value.Should().BeNull(because: "value isn't valid when foundResult is false");
        }

        [Test]
        public void StringValueGetsReturnedCorrectly()
        {
            var value = "TestValue";

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.TryGet<string?>("Test");
            result.foundResult.Should().BeTrue(because: "returned string is a found result");
            result.value.Should().Be("TestValue", because: "strings should get passed back");
        }

        [Test]
        public void IntValueGetsReturnedCorrectly()
        {
            var value = "10";

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.TryGet<int>("Test");
            result.foundResult.Should().BeTrue(because: "provided value should be 'found'");
            result.value.Should().Be(10, because: "ints should get parsed");
        }

        [Test]
        public void StringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.TryGet<byte[]>("Test");
            bytes.foundResult.Should().BeTrue(because: "provided value should be 'found'");
            bytes.value.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""), because: "non-intrinsic types should be handled as though they'd been JSON serialized");
        }

        [Test]
        public void EncryptedStringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.TryGet<byte[]>("Test", protectionLevel: ProtectionLevel.MachineKey);
            bytes.foundResult.Should().BeTrue(because: "provided value should be 'found'");
            bytes.value.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""), because: "encrypted non-intrinsic types should be handled as though they'd been JSON serialized");
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