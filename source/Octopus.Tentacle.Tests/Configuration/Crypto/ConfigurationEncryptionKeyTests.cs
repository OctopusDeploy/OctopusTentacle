using System;
using System.Runtime.Versioning;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [LinuxOrMacTest]
    [UnsupportedOSPlatform("windows")]
    public class ConfigurationEncryptionKeyTests
    {
        [Test]
        public void TellsTheUserToRunAsRootWhenTheKeyCannotBeCreated()
        {
            const string keyFilePath = "/var/lib/octopus/tentacle/Tentacle/keys/config-encryption.key";
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.When(f => f.EnsureDirectoryExists(Arg.Any<string>())).Do(_ => throw new UnauthorizedAccessException("Permission denied"));
            var key = new ConfigurationEncryptionKey(Substitute.For<ISystemLog>(), fileSystem, keyFilePath);

            Action createKey = () => key.CreateIfMissing();

            createKey.Should().Throw<InvalidOperationException>()
                .WithMessage($"*cannot create the configuration encryption key file `{keyFilePath}`*Run this command again as root*");
        }
    }
}
