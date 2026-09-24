using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    /// <summary>
    /// Proves that configuration written by Linux Tentacles before the versioned ciphertext format still decrypts,
    /// which is what makes upgrading an existing install safe.
    ///
    /// The ciphertext below was captured by running the encryptor exactly as it was before this change (commit
    /// 7b1504e, <c>LinuxMachineKeyEncryptor</c> iterating <c>LinuxMachineIdKey</c> then <c>LinuxGeneratedMachineKey</c>)
    /// against the fixed machine-id and key file here. That scheme used a fixed IV, so the output is deterministic
    /// and these values are pinned for good. Do not regenerate them with the current code: that would only prove the
    /// current code agrees with itself.
    /// </summary>
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

        /// <summary>
        /// The encryptor exactly as <see cref="MachineKeyEncryptor"/> composes it on Linux, over a fake file system
        /// holding a machine-id (or not) and a key file where a standard install keeps it.
        /// </summary>
        static IMachineKeyEncryptor CreateEncryptorAsComposedInProduction(bool machineIdPresent, string machineId = MachineId, string generatedKeyFileContents = GeneratedKeyFileContents)
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(machineIdPresent);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { machineId });
            fileSystem.FileExists(LinuxGeneratedMachineKey.StandardKeyFilePath).Returns(true);
            fileSystem.ReadAllText(LinuxGeneratedMachineKey.StandardKeyFilePath).Returns(generatedKeyFileContents);

            return MachineKeyEncryptor.CreateLinuxEncryptor(Substitute.For<ISystemLog>(), fileSystem);
        }

        [SetUp]
        public void SetUp()
        {
            // So the composition under test resolves the key file to /etc/octopus/machinekey whatever the host looks like.
            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, null);
        }

        [TestCaseSource(nameof(WrittenWithTheMachineIdKey))]
        public void ValuesWrittenByEarlierVersionsWithTheMachineIdKeyStillDecrypt(string plaintext, string legacyCiphertext)
        {
            CreateEncryptorAsComposedInProduction(machineIdPresent: true).Decrypt(legacyCiphertext).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheGeneratedKey))]
        public void ValuesWrittenByEarlierVersionsOnAHostWithoutAMachineIdStillDecrypt(string plaintext, string legacyCiphertext)
        {
            CreateEncryptorAsComposedInProduction(machineIdPresent: false).Decrypt(legacyCiphertext).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheGeneratedKey))]
        public void ValuesWrittenWithTheGeneratedKeyStillDecryptWhenAMachineIdHasSinceAppeared(string plaintext, string legacyCiphertext)
        {
            // e.g. a host that gained /etc/machine-id after Tentacle was configured: the machine-id key is tried first, fails, and the generated key is used.
            CreateEncryptorAsComposedInProduction(machineIdPresent: true).Decrypt(legacyCiphertext).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheMachineIdKey))]
        [TestCaseSource(nameof(WrittenWithTheGeneratedKey))]
        public void ValuesWrittenByEarlierVersionsAreReportedAsNeedingReEncryption(string plaintext, string legacyCiphertext)
        {
            CreateEncryptorAsComposedInProduction(machineIdPresent: true).RequiresReEncryption(legacyCiphertext).Should().BeTrue();
        }

        [TestCaseSource(nameof(WrittenWithTheMachineIdKey))]
        public void OnceReEncrypted_ValuesNoLongerDependOnTheMachineId(string plaintext, string legacyCiphertext)
        {
            var upgraded = CreateEncryptorAsComposedInProduction(machineIdPresent: true);
            var reEncrypted = upgraded.Encrypt(upgraded.Decrypt(legacyCiphertext));

            reEncrypted.Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            upgraded.RequiresReEncryption(reEncrypted).Should().BeFalse();

            // The same generated key but no machine-id at all, and a different machine-id: both must still read it,
            // because the machine-id plays no part in the current scheme.
            CreateEncryptorAsComposedInProduction(machineIdPresent: false).Decrypt(reEncrypted).Should().Be(plaintext);
            CreateEncryptorAsComposedInProduction(machineIdPresent: true, machineId: "00000000000000000000000000000000").Decrypt(reEncrypted).Should().Be(plaintext);
        }

        [TestCaseSource(nameof(WrittenWithTheGeneratedKey))]
        public void AnInstallThatRelocatedItsConfigurationHomeStillReadsValuesWrittenWithTheKeyAtTheStandardPath(string plaintext, string legacyCiphertext)
        {
            // Earlier versions ignored TentacleMachineConfigurationHomeDirectory for the key file, so it is at
            // /etc/octopus/machinekey; the current version keeps its key in the relocated home but must still read this.
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, "/home/octopus/.octopus");
            try
            {
                var fileSystem = Substitute.For<IOctopusFileSystem>();
                fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(false);
                fileSystem.FileExists(LinuxGeneratedMachineKey.StandardKeyFilePath).Returns(true);
                fileSystem.ReadAllText(LinuxGeneratedMachineKey.StandardKeyFilePath).Returns(GeneratedKeyFileContents);
                var relocatedKeyFile = "/home/octopus/.octopus/machinekey";
                string relocatedKey = null;
                fileSystem.FileExists(relocatedKeyFile).Returns(_ => relocatedKey != null);
                fileSystem.When(x => x.WriteAllTextOwnerOnly(relocatedKeyFile, Arg.Any<string>())).Do(ci => relocatedKey = ci.ArgAt<string>(1));
                fileSystem.ReadAllText(relocatedKeyFile).Returns(_ => relocatedKey);

                var encryptor = MachineKeyEncryptor.CreateLinuxEncryptor(Substitute.For<ISystemLog>(), fileSystem);

                encryptor.Decrypt(legacyCiphertext).Should().Be(plaintext);

                // And what it writes from now on uses a key of its own, in the relocated home.
                var reEncrypted = encryptor.Encrypt(plaintext);
                relocatedKey.Should().NotBeNull();
                fileSystem.DidNotReceive().WriteAllTextOwnerOnly(LinuxGeneratedMachineKey.StandardKeyFilePath, Arg.Any<string>());
                encryptor.Decrypt(reEncrypted).Should().Be(plaintext);
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, null);
            }
        }

        [Test]
        public void NewValuesCannotBeReadWithTheMachineIdAlone()
        {
            // The scenario this change exists for: someone with the (public) image's machine-id and a copy of tentacle.config.
            var thisMachine = CreateEncryptorAsComposedInProduction(machineIdPresent: true);
            var encrypted = thisMachine.Encrypt("the certificate");

            var attackerWithTheSameImage = CreateEncryptorAsComposedInProduction(machineIdPresent: true,
                generatedKeyFileContents: "YW55IG90aGVyIGtleSBvZiB0aGlydHktdHdvIGJ5dGVz.b3RoZXIgaXY=");

            attackerWithTheSameImage.Invoking(x => x.Decrypt(encrypted)).Should().Throw<Exception>();
        }
    }
}
