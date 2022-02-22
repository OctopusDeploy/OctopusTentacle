using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ProxyConfigurationCommandFixture : CommandFixture<ProxyConfigurationCommand>
    {
        private string homeDirectory;
        private string configFile;
        private OctopusPhysicalFileSystem octopusFileSystem;
        private IApplicationInstanceSelector applicationInstanceSelector;

        [SetUp]
        public void SetupForEachTest()
        {
            octopusFileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            homeDirectory = octopusFileSystem.CreateTemporaryDirectory();
            configFile = $"{homeDirectory}\\File.config";

            applicationInstanceSelector = Substitute.For<IApplicationInstanceSelector>();
            applicationInstanceSelector.Current.Returns(info => new ApplicationInstanceConfiguration(null, null!, null!, null!));

            octopusFileSystem.AppendToFile(configFile,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<octopus-settings xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "</octopus-settings>");
        }

        [TearDown]
        public void TearDownAfterEachTest()
        {
            if (File.Exists(configFile)) File.Delete(configFile);
        }

        [Test]
        public void ToggleTheProxy()
        {
            var config = new Lazy<IWritableProxyConfiguration>(() => new WritableProxyConfiguration(new XmlFileKeyValueStore(octopusFileSystem, configFile)));
            const string expectedProxyHost = "127.0.0.1";
            const string expectedUsername = "yoda";
            const string expectedPassword = "do or do not, there is no try";
            const int expectedProxyPort = 8888;

            Command = new ProxyConfigurationCommand(config, applicationInstanceSelector, Substitute.For<ISystemLog>(), Substitute.For<ILogFileOnlyLogger>());

            EnableACustomProxy();
            config.Value.UseDefaultProxy.Should().BeFalse("we're using a custom proxy now");
            config.Value.CustomProxyHost.Should().Be(expectedProxyHost, "we've supplied a proxy host");
            config.Value.CustomProxyPort.Should().Be(expectedProxyPort, "we've supplied a proxy port");
            config.Value.CustomProxyUsername.Should().Be(expectedUsername, "we've supplied a proxy username");
            config.Value.CustomProxyPassword.Should().Be(expectedPassword, "we've supplied a proxy password");

            DisableTheProxy();
            config.Value.UseDefaultProxy.Should().BeFalse("we've disabled the proxy altogether");
            config.Value.CustomProxyHost.Should().BeNullOrEmpty("the proxyHost setting should be cleared when we disable the proxy");
            config.Value.CustomProxyPort.Should().Be(expectedProxyPort, "the port is now ignored");

            EnableTheDefaultProxy();
            config.Value.CustomProxyPort.Should().Be(expectedProxyPort, "the port is still ignored, even though we're enabling the default proxy");

            void EnableACustomProxy()
            {
                Start("--proxyEnable=true", $"--proxyHost={expectedProxyHost}", $"--proxyPort={expectedProxyPort}", $"--proxyUsername={expectedUsername}", $"--proxyPassword={expectedPassword}");
            }

            void DisableTheProxy()
            {
                Start("--proxyEnable=false");
            }
        }

        [Test]
        public void TurnOnDefaultProxy()
        {
            var config = new Lazy<IWritableProxyConfiguration>(() => new WritableProxyConfiguration(new XmlFileKeyValueStore(octopusFileSystem, configFile)));
            Command = new ProxyConfigurationCommand(config, applicationInstanceSelector, Substitute.For<ISystemLog>(), Substitute.For<ILogFileOnlyLogger>());

            EnableTheDefaultProxy();

            config.Value.UseDefaultProxy.Should().BeTrue("we've enabled the proxy without supplying any proxy settings, so we should use the default one");
            config.Value.CustomProxyHost.Should().BeNullOrEmpty("we haven't supplied a proxy host");
            config.Value.CustomProxyPort.Should().Be(80, "we haven't supplied a port number, so use the default value of 80");
        }

        [Test]
        public void UseACustomHostAndIgnoreHttpAndPort()
        {
            var config = new Lazy<IWritableProxyConfiguration>(() => new WritableProxyConfiguration(new XmlFileKeyValueStore(octopusFileSystem, configFile)));
            Command = new ProxyConfigurationCommand(config, applicationInstanceSelector, Substitute.For<ISystemLog>(), Substitute.For<ILogFileOnlyLogger>());

            EnableAnIncorrectlySuppliedProxyHost();

            config.Value.UseDefaultProxy.Should().BeFalse("we're using a custom proxy now");
            config.Value.CustomProxyHost.Should().Be("127.0.0.1", "we supplied a valid URL, however it should be stripped of protocol and port information");
            config.Value.CustomProxyPort.Should().Be(80, "we haven't supplied a port number, so use the default value of 80");

            void EnableAnIncorrectlySuppliedProxyHost()
            {
                Start("--proxyEnable=true", "--proxyHost=http://127.0.0.1:8888");
            }
        }

        private void EnableTheDefaultProxy()
        {
            Start("--proxyEnable=true");
        }
    }
}