#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    // Ciphertext captured from the pre-versioned encryptor (commit 7b1504e), which used a fixed IV.
    // Do not regenerate it with the current code: that would only prove the current code agrees with itself.
    [LinuxOrMacTest]
    public class LegacyCiphertextCompatibilityTests
    {
        const string MachineId = "816490f117f14c7a8fa2697781ed795b";
        const string GeneratedKeyPath = "/etc/octopus/machinekey";
        const string GeneratedKeyFileContents = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==";

        const string CertificateSizedText = "AAcOFRwjKjE4P0ZNVFtiaXB3foWMk5qhqK+2vcTL0tng5+71/AMKERgfJi00O0JJUFdeZWxzeoGIj5adpKuyucDHztXc4+rx+P8GDRQbIikwNz5FTFNaYWhvdn2Ei5KZoKeutbzDytHY3+bt9PsCCRAXHiUsMzpBSE9WXWRrcnmAh46VnKOqsbi/xs3U2+Lp8Pf+BQwTGiEoLzY9REtSWWBnbnV8g4qRmJ+mrbS7wsnQ197l7PP6AQgPFh0kKzI5QEdOVVxjanF4f4aNlJuiqbC3vsXM09rh6O/2/QQLEhkgJy41PENKUVhfZm10e4KJkJeepayzusHIz9bd5Ovy+QAHDhUcIyoxOD9GTVRbYmlwd36FjJOaoaivtr3Ey9LZ4Ofu9fwDChEYHyYt";

        [TestCase("FooBar", "yspqcK7xCvd5c+1OSB7kxw==")]
        [TestCase("true", "lfWdFxOQukFHfUs767dBOA==")]
        [TestCase("123", "c5E+1E8Q73XGrrSEWenz/A==")]
        [TestCase("pässwörd 🐙 with spaces & symbols!", "nMmwC8Uzkg/bEwaDChRL86dTMzY4gCyveDi0qCjOWNENjQzd3wYS8lJwMeGXNdEy")]
        [TestCase(CertificateSizedText, "2qbSDqJXa0/Bz1SzohM21m3dg3mIPBjj4mgbfve/WW1cZFNAgccjx+Hot6Qp8/TC8Su4rLS8yp6i+WY4J8x7ga6DbFVD4pQkjy8ag37nxwBos4llfCRhRqV0LZWol2U66+znAfTscepLI1nVWgtJtloYLGky/6QDjvMQQG9S/MmQsBzANfI3lQG8ftYK/ilU6xX7eJwvWViEG3HI/3loc7k9jbJT13JF4f8NpI3yEH4Q4VCWY9j3z+ZK/2kImeqRDfv9cl4roUdtBQ4I333taVovDwILKqq2a6QPRZiAVJ34in2CrrJv2dTWgaQHsU74upUlKiKrZs6LgpZVP7y+5W/o52te+dvwCBVBqFmxFUmpk/1gAMUiqZ0fEXrz8GlyS4dcIXlFUwUXcdUb1E2ldqDMRr2zDHP0ATwdBcKm9yM4sJKrGhrSksl7U7chM4CpRVTUf1tWvU3SjcQ00haPM+RZP2pWDnkDZhkrDRfpQke/qFL8wG9RySpzWDF+zsN0yUxZJgqUvy9cHb/yHphxTqyEmmoGcAuzOaA9a2yYGPs=")]
        public void WhenAValueWasWrittenWithTheMachineIdKeyThenItStillDecrypts(string plaintext, string legacyCiphertext)
        {
            // Arrange
            var encryptor = CreateEncryptor(machineIdPresent: true, generatedKeyPresent: false);

            // Act
            var decrypted = encryptor.Decrypt(legacyCiphertext);

            // Assert
            decrypted.Should().Be(plaintext);
        }

        [TestCase("FooBar", "sU7FAsi9LrgjCISbdFN4Kg==")]
        [TestCase("true", "ks2Nbbl8OonD1r2CFim+VA==")]
        [TestCase("123", "QL+mcAjSyVWEsVPlQeNvqA==")]
        [TestCase("pässwörd 🐙 with spaces & symbols!", "cVzJO4WpcDeZTWcG91jyln6rFJvjf08RkpiAb+FUMcOnzT8uB5emsPmEKS8vyg+p")]
        [TestCase(CertificateSizedText, "NhUpBIgpSxmvYSXfn+cn5lKK5xB1LDf0NdKogqm1MAn4XQ8p84KJwVzEevn/T7Glk9Lirqa/RtUM9HuEEadDZ6XKhbnBdUJGLx1CFfHHw/3L3IProqBX/mD+W0SVIvnI54iq5PFdwtSSOH3a6PC+DT92rmXjy4/YoNzuGKkNcMoP/GG3g6URPHGdMq3NMs7t3wD7k8MbvU96F5qlTdqiwgILViruLJGZhECb99GmTdJ/PjCeFE87+up+vZllk1WVboGAIn6Vf+1Pgd7tDHgczDHZvgKIeP1k5gwH9dXzSd/OweG7eW7Yux0HIcI1oPMDcjx57ldDzYwhgIX5jPkMGFP3J4guwCIaSQTEyMLP85ZervJ0uWlAq/rlIK71dQ5c6wvFIVKkKeSo+qWReMRi9G4rMaelnagNTwUqBBfy+nff+tbvPrLV+CmKDCEgA+g1d7kF7+y/kBJ4NaZOpo6jBUVds/sV7ieVZhcUmHN2eML4/Zur5KyHvHsDnOWU3jgBxQ6oJJQB4yePmWivAXWlE4ISpIvM5i76XYqLbEJ+lbQ=")]
        public void WhenAValueWasWrittenWithoutAMachineIdThenItStillDecrypts(string plaintext, string legacyCiphertext)
        {
            // Arrange
            var encryptor = CreateEncryptor(machineIdPresent: false, generatedKeyPresent: true);

            // Act
            var decrypted = encryptor.Decrypt(legacyCiphertext);

            // Assert
            decrypted.Should().Be(plaintext);
        }

        static IMachineKeyEncryptor CreateEncryptor(bool machineIdPresent, bool generatedKeyPresent)
        {
            var files = new Dictionary<string, string>();
            if (generatedKeyPresent)
                files[GeneratedKeyPath] = GeneratedKeyFileContents;

            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists(Arg.Any<string>()).Returns(ci => files.ContainsKey(ci.ArgAt<string>(0)));
            fileSystem.ReadAllText(Arg.Any<string>()).Returns(ci => files[ci.ArgAt<string>(0)]);
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(machineIdPresent);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { MachineId });

            return MachineKeyEncryptor.CreateLinuxEncryptor(Substitute.For<ISystemLog>(), fileSystem, ConfigurationEncryptionKey.KeyFilePathFor("Tentacle"), GeneratedKeyPath);
        }
    }
}
#endif
