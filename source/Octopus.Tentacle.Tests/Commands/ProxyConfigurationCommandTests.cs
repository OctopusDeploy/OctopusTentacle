using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Commands
{
    public class ProxyConfigurationCommandTests
    {
        [Test]
        public void ToggleTheProxy()
        {
            var octopusFileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            using var homeDirectory = new TemporaryDirectory(octopusFileSystem);
            var config = NewProxyConfiguration(octopusFileSystem, homeDirectory);
            const string expectedProxyHost = "127.0.0.1";
            const string expectedUsername = "yoda";
            const string expectedPassword = "do or do not, there is no try";
            const int expectedProxyPort = 8888;

            var command = CommandBuilder.ProxyConfiguration(config);

            EnableACustomProxy();
            config.Value.UseDefaultProxy.Should().BeFalse(because: "we're using a custom proxy now");
            config.Value.CustomProxyHost.Should().Be(expectedProxyHost, because: "we've supplied a proxy host");
            config.Value.CustomProxyPort.Should().Be(expectedProxyPort, because: "we've supplied a proxy port");
            config.Value.CustomProxyUsername.Should().Be(expectedUsername, because: "we've supplied a proxy username");
            config.Value.CustomProxyPassword.Should().Be(expectedPassword, because: "we've supplied a proxy password");

            DisableTheProxy();
            config.Value.UseDefaultProxy.Should().BeFalse(because: "we've disabled the proxy altogether");
            config.Value.CustomProxyHost.Should().BeNullOrEmpty(because: "the proxyHost setting should be cleared when we disable the proxy");
            config.Value.CustomProxyPort.Should().Be(expectedProxyPort, because: "the port is now ignored");

            EnableTheDefaultProxy(command);
            config.Value.CustomProxyPort.Should().Be(expectedProxyPort, because: "the port is still ignored, even though we're enabling the default proxy");

            void EnableACustomProxy()
            {
                command.StartWith("--proxyEnable=true", $"--proxyHost={expectedProxyHost}", $"--proxyPort={expectedProxyPort}", $"--proxyUsername={expectedUsername}", $"--proxyPassword={expectedPassword}");
            }

            void DisableTheProxy()
            {
                command.StartWith("--proxyEnable=false");
            }
        }

        [Test]
        public void TurnOnDefaultProxy()
        {
            var octopusFileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            using var homeDirectory = new TemporaryDirectory(octopusFileSystem);
            var config = NewProxyConfiguration(octopusFileSystem, homeDirectory);
            var command = CommandBuilder.ProxyConfiguration(config);

            EnableTheDefaultProxy(command);

            config.Value.UseDefaultProxy.Should().BeTrue(because: "we've enabled the proxy without supplying any proxy settings, so we should use the default one");
            config.Value.CustomProxyHost.Should().BeNullOrEmpty(because: "we haven't supplied a proxy host");
            config.Value.CustomProxyPort.Should().Be(80, because: "we haven't supplied a port number, so use the default value of 80");
        }

        [Test]
        public void UseACustomHostAndIgnoreHttpAndPort()
        {
            var octopusFileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            using var homeDirectory = new TemporaryDirectory(octopusFileSystem);
            var config = NewProxyConfiguration(octopusFileSystem, homeDirectory);
            var command = CommandBuilder.ProxyConfiguration(config);

            EnableAnIncorrectlySuppliedProxyHost();

            config.Value.UseDefaultProxy.Should().BeFalse(because: "we're using a custom proxy now");
            config.Value.CustomProxyHost.Should().Be("127.0.0.1", because: "we supplied a valid URL, however it should be stripped of protocol and port information");
            config.Value.CustomProxyPort.Should().Be(80, because: "we haven't supplied a port number, so use the default value of 80");

            void EnableAnIncorrectlySuppliedProxyHost()
            {
                command.StartWith("--proxyEnable=true", "--proxyHost=http://127.0.0.1:8888");
            }
        }

        static void EnableTheDefaultProxy(ProxyConfigurationCommand command)
        {
            command.StartWith("--proxyEnable=true");
        }

        static Lazy<IWritableProxyConfiguration> NewProxyConfiguration(OctopusPhysicalFileSystem octopusFileSystem, TemporaryDirectory homeDirectory)
        {
            var configFile = Path.Combine(homeDirectory.DirectoryPath, "File.config");
            octopusFileSystem.AppendToFile(configFile,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<octopus-settings xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "</octopus-settings>");

            return new Lazy<IWritableProxyConfiguration>(() => new WritableProxyConfiguration(new XmlFileKeyValueStore(octopusFileSystem, configFile, encryptor: TestMachineKeyEncryptor.Current)));
        }
    }
}
