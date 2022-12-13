#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;

namespace Octopus.Tentacle.Tests.Configuration
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
            result.foundResult.Should().BeFalse("defaulted value should tell you it wasn't found, and is thus the default");
            result.value.Should().BeNull("value isn't valid when foundResult is false");
        }

        [Test]
        public void StringValueGetsReturnedCorrectly()
        {
            var value = "TestValue";

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.TryGet<string?>("Test");
            result.foundResult.Should().BeTrue("returned string is a found result");
            result.value.Should().Be("TestValue", "strings should get passed back");
        }

        [Test]
        public void IntValueGetsReturnedCorrectly()
        {
            var value = "10";

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var result = store.TryGet<int>("Test");
            result.foundResult.Should().BeTrue("provided value should be 'found'");
            result.value.Should().Be(10, "ints should get parsed");
        }

        [Test]
        public void StringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.TryGet<byte[]>("Test");
            bytes.foundResult.Should().BeTrue("provided value should be 'found'");
            bytes.value.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""), "non-intrinsic types should be handled as though they'd been JSON serialized");
        }

        [Test]
        public void EncryptedStringValueGetsConvertedToByteArrayCorrectly()
        {
            var value = Convert.ToBase64String(GenerateValue());

            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns(value);
            var store = new InMemoryKeyValueStore(mapper);

            var bytes = store.TryGet<byte[]>("Test", ProtectionLevel.MachineKey);
            bytes.foundResult.Should().BeTrue("provided value should be 'found'");
            bytes.value.Should().BeEquivalentTo(JsonConvert.DeserializeObject<byte[]>($"\"{value}\""), "encrypted non-intrinsic types should be handled as though they'd been JSON serialized");
        }

        [Test]
        public void ComplexTypeGetsHandledCorrectly()
        {
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.GetConfigurationValue("Test").Returns("[{\"SettingA\":\"some value\", \"SomethingElse\":12}]");
            var store = new InMemoryKeyValueStore(mapper);

            var settings = store.TryGet<TestConfig[]>("Test");
            settings.value.Single().SettingA.Should().Be("some value", "strings should get parsed");
            settings.value.Single().SomethingElse.Should().Be(12, "ints should get parsed");
        }

        class TestConfig
        {
            public string SettingA { get; set; } = string.Empty;
            public int SomethingElse { get; set; }
        }


        static byte[] GenerateValue()
        {
            var key = new byte[16];
            using (var provider = new RNGCryptoServiceProvider())
            {
                provider.GetBytes(key);
            }

            return key;
        }
    }
}