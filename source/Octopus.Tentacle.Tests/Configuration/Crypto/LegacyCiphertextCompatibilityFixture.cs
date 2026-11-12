#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    // The ciphertext below was captured from the encryptor as it was before the versioned format (commit 7b1504e), which
    // used a fixed IV and so is deterministic. Do not regenerate it with the current code: that would only prove the
    // current code agrees with itself.
    [TestFixture]
    public class LegacyCiphertextCompatibilityFixture
    {
        const string MachineId = "816490f117f14c7a8fa2697781ed795b";
        const string GeneratedKeyFileContents = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==";

        const string ShortText = "FooBar";
        const string SerialisedBool = "true";
        const string SerialisedInt = "123";
        const string UnicodeText = "pässwörd 🐙 with spaces & symbols!";
        static readonly string CertificateSizedText = Convert.ToBase64String(Enumerable.Range(0, 300).Select(i => (byte)(i * 7)).ToArray());

        public static IEnumerable<TestCaseData> WrittenWithTheMachineIdKey()
        {
            yield return new TestCaseData(ShortText, "yspqcK7xCvd5c+1OSB7kxw==").SetName("{m}(machine-id, short text)");
            yield return new TestCaseData(SerialisedBool, "lfWdFxOQukFHfUs767dBOA==").SetName("{m}(machine-id, serialised bool)");
            yield return new TestCaseData(SerialisedInt, "c5E+1E8Q73XGrrSEWenz/A==").SetName("{m}(machine-id, serialised int)");
            yield return new TestCaseData(UnicodeText, "nMmwC8Uzkg/bEwaDChRL86dTMzY4gCyveDi0qCjOWNENjQzd3wYS8lJwMeGXNdEy").SetName("{m}(machine-id, unicode)");
            yield return new TestCaseData(CertificateSizedText,
                "2qbSDqJXa0/Bz1SzohM21m3dg3mIPBjj4mgbfve/WW1cZFNAgccjx+Hot6Qp8/TC8Su4rLS8yp6i+WY4J8x7ga6DbFVD4pQkjy8ag37nxwBos4llfCRhRqV0LZWol2U66+znAfTscepLI1nVWgtJtloYLGky/6QDjvMQQG9S/MmQsBzANfI3lQG8ftYK/ilU6xX7eJwvWViEG3HI/3loc7k9jbJT13JF4f8NpI3yEH4Q4VCWY9j3z+ZK/2kImeqRDfv9cl4roUdtBQ4I333taVovDwILKqq2a6QPRZiAVJ34in2CrrJv2dTWgaQHsU74upUlKiKrZs6LgpZVP7y+5W/o52te+dvwCBVBqFmxFUmpk/1gAMUiqZ0fEXrz8GlyS4dcIXlFUwUXcdUb1E2ldqDMRr2zDHP0ATwdBcKm9yM4sJKrGhrSksl7U7chM4CpRVTUf1tWvU3SjcQ00haPM+RZP2pWDnkDZhkrDRfpQke/qFL8wG9RySpzWDF+zsN0yUxZJgqUvy9cHb/yHphxTqyEmmoGcAuzOaA9a2yYGPs=")
                .SetName("{m}(machine-id, certificate-sized)");
        }

        public static IEnumerable<TestCaseData> WrittenWithTheGeneratedKey()
        {
            yield return new TestCaseData(ShortText, "sU7FAsi9LrgjCISbdFN4Kg==").SetName("{m}(generated, short text)");
            yield return new TestCaseData(SerialisedBool, "ks2Nbbl8OonD1r2CFim+VA==").SetName("{m}(generated, serialised bool)");
            yield return new TestCaseData(SerialisedInt, "QL+mcAjSyVWEsVPlQeNvqA==").SetName("{m}(generated, serialised int)");
            yield return new TestCaseData(UnicodeText, "cVzJO4WpcDeZTWcG91jyln6rFJvjf08RkpiAb+FUMcOnzT8uB5emsPmEKS8vyg+p").SetName("{m}(generated, unicode)");
            yield return new TestCaseData(CertificateSizedText,
                "NhUpBIgpSxmvYSXfn+cn5lKK5xB1LDf0NdKogqm1MAn4XQ8p84KJwVzEevn/T7Glk9Lirqa/RtUM9HuEEadDZ6XKhbnBdUJGLx1CFfHHw/3L3IProqBX/mD+W0SVIvnI54iq5PFdwtSSOH3a6PC+DT92rmXjy4/YoNzuGKkNcMoP/GG3g6URPHGdMq3NMs7t3wD7k8MbvU96F5qlTdqiwgILViruLJGZhECb99GmTdJ/PjCeFE87+up+vZllk1WVboGAIn6Vf+1Pgd7tDHgczDHZvgKIeP1k5gwH9dXzSd/OweG7eW7Yux0HIcI1oPMDcjx57ldDzYwhgIX5jPkMGFP3J4guwCIaSQTEyMLP85ZervJ0uWlAq/rlIK71dQ5c6wvFIVKkKeSo+qWReMRi9G4rMaelnagNTwUqBBfy+nff+tbvPrLV+CmKDCEgA+g1d7kF7+y/kBJ4NaZOpo6jBUVds/sV7ieVZhcUmHN2eML4/Zur5KyHvHsDnOWU3jgBxQ6oJJQB4yePmWivAXWlE4ISpIvM5i76XYqLbEJ+lbQ=")
                .SetName("{m}(generated, certificate-sized)");
        }

        const string ConfigurationFile = "/etc/octopus/Tentacle/tentacle-Tentacle.config";
        const string NewKeyFile = "/etc/octopus/Tentacle/config-encryption.key";

        FakeKeyFiles files;

        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null);
            files = new FakeKeyFiles();
            files.Contents[LinuxGeneratedMachineKey.LegacyKeyFilePath] = GeneratedKeyFileContents;
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null);
        }

        IMachineKeyEncryptor CreateEncryptorAsComposedInProduction(bool machineIdPresent, string machineId = MachineId)
        {
            var fileSystem = files.CreateFileSystem();
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(machineIdPresent);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { machineId });

            return MachineKeyEncryptor.CreateLinuxEncryptor(Substitute.For<ISystemLog>(), fileSystem, LinuxGeneratedMachineKey.KeyFilePathFor(ConfigurationFile), ConfigurationFile);
        }

        [TestCaseSource(nameof(WrittenWithTheMachineIdKey))]
        public void ValuesWrittenByEarlierVersionsWithTheMachineIdKeyStillDecrypt(string plaintext, string legacyCiphertext)
        {
            files.Contents.Remove(LinuxGeneratedMachineKey.LegacyKeyFilePath);

            CreateEncryptorAsComposedInProduction(machineIdPresent: true).Decrypt(legacyCiphertext).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheGeneratedKey))]
        public void ValuesWrittenByEarlierVersionsOnAHostWithoutAMachineIdStillDecrypt(string plaintext, string legacyCiphertext)
        {
            CreateEncryptorAsComposedInProduction(machineIdPresent: false).Decrypt(legacyCiphertext).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheMachineIdKey))]
        public void OnceReEncrypted_ValuesNoLongerDependOnTheMachineIdOrTheLegacyKey(string plaintext, string legacyCiphertext)
        {
            var upgraded = CreateEncryptorAsComposedInProduction(machineIdPresent: true);
            var reEncrypted = upgraded.Encrypt(upgraded.Decrypt(legacyCiphertext));

            reEncrypted.Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            upgraded.RequiresReEncryption(reEncrypted).Should().BeFalse();

            files.Contents.Remove(LinuxGeneratedMachineKey.LegacyKeyFilePath);
            CreateEncryptorAsComposedInProduction(machineIdPresent: false).Decrypt(reEncrypted).Should().Be(plaintext);
            CreateEncryptorAsComposedInProduction(machineIdPresent: true, machineId: "00000000000000000000000000000000").Decrypt(reEncrypted).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheMachineIdKey))]
        public void TheNewKeyIsCreatedBesideTheConfigurationAndTheLegacyKeyFileIsNeverCreatedOrChanged(string plaintext, string legacyCiphertext)
        {
            files.Contents.Remove(LinuxGeneratedMachineKey.LegacyKeyFilePath);
            var upgraded = CreateEncryptorAsComposedInProduction(machineIdPresent: true);

            upgraded.Encrypt(upgraded.Decrypt(legacyCiphertext));

            files.Contents.Keys.Should().BeEquivalentTo(new[] { NewKeyFile });
        }

        [Test]
        public void NewValuesCannotBeReadWithTheMachineIdAlone()
        {
            // Someone with the image's public machine-id and a copy of tentacle.config.
            var thisMachine = CreateEncryptorAsComposedInProduction(machineIdPresent: true);
            var encrypted = thisMachine.Encrypt("the certificate");

            files.Contents[NewKeyFile] = "YW55IG90aGVyIGtleSBvZiB0aGlydHktdHdvIGJ5dGU=.b3RoZXIgaXYgb2YgMTYgYg==";
            var attackerWithTheSameImage = CreateEncryptorAsComposedInProduction(machineIdPresent: true);

            attackerWithTheSameImage.Invoking(x => x.Decrypt(encrypted)).Should().Throw<Exception>();
        }

        class FakeKeyFiles
        {
            public Dictionary<string, string> Contents { get; } = new();

            public IOctopusFileSystem CreateFileSystem()
            {
                var fileSystem = Substitute.For<IOctopusFileSystem>();
                fileSystem.FileExists(Arg.Any<string>()).Returns(ci => Contents.ContainsKey(ci.ArgAt<string>(0)));
                fileSystem.ReadAllText(Arg.Any<string>()).Returns(ci => Contents[ci.ArgAt<string>(0)]);
                fileSystem.TryCreateFileOwnerOnly(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(ci => Contents.TryAdd(ci.ArgAt<string>(0), Encoding.ASCII.GetString(ci.ArgAt<byte[]>(1))));
                return fileSystem;
            }
        }
    }
}
#endif
