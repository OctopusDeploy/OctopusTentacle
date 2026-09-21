using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Configuration.Crypto;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class ProtectedSettingsMigratorFixture
    {
        const string Certificate = "MIIC...the certificate...";
        const string ProxyPassword = "pässwörd 🐙";
        const string PollingProxyPassword = "another one";

        string configurationFile;
        OctopusPhysicalFileSystem fileSystem;
        LinuxMachineKeyEncryptor encryptor;
        XmlFileKeyValueStore legacyStore;
        XmlFileKeyValueStore store;
        IApplicationInstanceSelector selector;
        ISystemLog log;

        [SetUp]
        public void SetUp()
        {
            configurationFile = Path.GetTempFileName();
            fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");

            var legacyKey = new InMemoryCryptoKeyNixSource();
            encryptor = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), new InMemoryCryptoKeyNixSource(), new[] { legacyKey });
            legacyStore = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: new LegacySchemeEncryptor(legacyKey));
            store = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor);

            selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(new ApplicationInstanceConfiguration("Tentacle", configurationFile, store, store));
            log = Substitute.For<ISystemLog>();
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(configurationFile);
        }

        ProtectedSettingsMigrator CreateMigrator()
            => new ProtectedSettingsMigrator(selector, log);

        void SeedAnUpgradedInstall()
        {
            legacyStore.Set<string>("Tentacle.Certificate", Certificate, ProtectionLevel.MachineKey);
            legacyStore.Set<string>("Tentacle.CertificateThumbprint", "ABCDEF");
            legacyStore.Set<string>("Octopus.Proxy.ProxyPassword", ProxyPassword, ProtectionLevel.MachineKey);
            legacyStore.Set<string>("Octopus.Server.Proxy.ProxyPassword", PollingProxyPassword, ProtectionLevel.MachineKey);
            legacyStore.Set("Tentacle.Services.PortNumber", 10933);
            legacyStore.Set<string>("Tentacle.Deployment.ApplicationDirectory", "/home/Octopus/Applications");
        }

        [Test]
        public void EveryLegacyProtectedSettingIsReEncryptedAndStillReadsBack()
        {
            SeedAnUpgradedInstall();

            CreateMigrator().ReEncryptLegacyProtectedSettings();

            var reloaded = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor);
            foreach (var name in ProtectedSettingsMigrator.ProtectedSettingNames)
                RawValue(name).Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix, name);
            reloaded.Get<string>("Tentacle.Certificate", protectionLevel: ProtectionLevel.MachineKey).Should().Be(Certificate);
            reloaded.Get<string>("Octopus.Proxy.ProxyPassword", protectionLevel: ProtectionLevel.MachineKey).Should().Be(ProxyPassword);
            reloaded.Get<string>("Octopus.Server.Proxy.ProxyPassword", protectionLevel: ProtectionLevel.MachineKey).Should().Be(PollingProxyPassword);

            log.Received(3).Info(Arg.Is<string>(m => m.StartsWith("Re-encrypted the protected setting")));
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void UnprotectedSettingsAreUntouched()
        {
            SeedAnUpgradedInstall();

            CreateMigrator().ReEncryptLegacyProtectedSettings();

            RawValue("Tentacle.CertificateThumbprint").Should().Be("ABCDEF");
            RawValue("Tentacle.Services.PortNumber").Should().Be("10933");
            RawValue("Tentacle.Deployment.ApplicationDirectory").Should().Be("/home/Octopus/Applications");
        }

        [Test]
        public void RunningAgainIsANoOp()
        {
            SeedAnUpgradedInstall();
            CreateMigrator().ReEncryptLegacyProtectedSettings();
            var after = File.ReadAllText(configurationFile);
            log.ClearReceivedCalls();

            CreateMigrator().ReEncryptLegacyProtectedSettings();

            File.ReadAllText(configurationFile).Should().Be(after);
            log.DidNotReceive().Info(Arg.Any<string>());
        }

        [Test]
        public void AFreshInstallIsANoOp()
        {
            store.Set<string>("Tentacle.Certificate", Certificate, ProtectionLevel.MachineKey);
            var before = File.ReadAllText(configurationFile);

            CreateMigrator().ReEncryptLegacyProtectedSettings();

            File.ReadAllText(configurationFile).Should().Be(before);
            log.DidNotReceive().Info(Arg.Any<string>());
        }

        [Test]
        public void SettingsThatAreNotPresentAreSkipped()
        {
            legacyStore.Set<string>("Tentacle.Certificate", Certificate, ProtectionLevel.MachineKey);

            CreateMigrator().ReEncryptLegacyProtectedSettings();

            log.Received(1).Info(Arg.Is<string>(m => m.Contains("'Tentacle.Certificate'")));
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void ASettingThatCannotBeRewrittenIsLoggedAsAWarningAndTheOthersAreStillDone()
        {
            var failingStore = Substitute.For<IWritableKeyValueStore, IReEncryptingKeyValueStore>();
            ((IReEncryptingKeyValueStore)failingStore).ReEncryptIfLegacy("Tentacle.Certificate").Throws(new IOException("Read-only file system"));
            ((IReEncryptingKeyValueStore)failingStore).ReEncryptIfLegacy("Octopus.Proxy.ProxyPassword").Returns(true);
            ((IReEncryptingKeyValueStore)failingStore).ReEncryptIfLegacy("Octopus.Server.Proxy.ProxyPassword").Returns(false);
            selector.Current.Returns(new ApplicationInstanceConfiguration("Tentacle", configurationFile, failingStore, failingStore));

            CreateMigrator().Invoking(x => x.ReEncryptLegacyProtectedSettings()).Should().NotThrow();

            log.Received(1).Warn(Arg.Any<IOException>(), Arg.Is<string>(m => m.Contains("'Tentacle.Certificate'") && m.Contains("still readable")));
            log.Received(1).Info(Arg.Is<string>(m => m.Contains("'Octopus.Proxy.ProxyPassword'")));
            ((IReEncryptingKeyValueStore)failingStore).Received(1).ReEncryptIfLegacy("Octopus.Server.Proxy.ProxyPassword");
        }

        [Test]
        public void AStoreThatCannotReEncryptIsIgnored()
        {
            // e.g. the Kubernetes agent's ConfigMap store, which has its own per-agent key.
            var configMapStore = Substitute.For<IWritableKeyValueStore>();
            selector.Current.Returns(new ApplicationInstanceConfiguration("Tentacle", null, configMapStore, configMapStore));

            CreateMigrator().Invoking(x => x.ReEncryptLegacyProtectedSettings()).Should().NotThrow();

            log.DidNotReceive().Info(Arg.Any<string>());
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void NoWritableConfigurationIsIgnored()
        {
            selector.Current.Returns(new ApplicationInstanceConfiguration("Tentacle", null, null, null));

            CreateMigrator().Invoking(x => x.ReEncryptLegacyProtectedSettings()).Should().NotThrow();
        }

        string RawValue(string name)
            => XDocument.Load(configurationFile).Root!.Elements("set").Single(e => (string)e.Attribute("key") == name).Value;
    }
}
