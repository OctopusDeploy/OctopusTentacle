using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ProxyConfigurationCommandFixture : CommandFixture<ProxyConfigurationCommand>
    {
        [Test]
        public void TurnOffProxy()
        {
            var config = new Lazy<IProxyConfiguration>(() => new ProxyConfiguration(Substitute.For<IKeyValueStore>()));
            Command = new ProxyConfigurationCommand(config, Substitute.For<IApplicationInstanceSelector>(), Substitute.For<ILog>());

            Start("--proxyEnable=false");

            config.Value.UseDefaultProxy.Should().BeFalse();
            config.Value.CustomProxyHost.Should().BeNullOrEmpty();
        }

        [Test]
        public void TurnOnDefaultProxy()
        {
            var config = new Lazy<IProxyConfiguration>(() => new ProxyConfiguration(new XmlConsoleKeyValueStore()));
            Command = new ProxyConfigurationCommand(config, Substitute.For<IApplicationInstanceSelector>(), Substitute.For<ILog>());

            Start("--proxyEnable=true");

            config.Value.UseDefaultProxy.Should().BeTrue();
            config.Value.CustomProxyHost.Should().BeNullOrEmpty();
        }

        [Test]
        public void UseACustomHostAndIgnoreHttpAndPort()
        {
            var config = new Lazy<IProxyConfiguration>(() => new ProxyConfiguration(new XmlConsoleKeyValueStore()));
            Command = new ProxyConfigurationCommand(config, Substitute.For<IApplicationInstanceSelector>(), Substitute.For<ILog>());

            Start("--proxyEnable=true", "--proxyHost=http://127.0.0.1:8888");

            config.Value.UseDefaultProxy.Should().BeFalse();
            config.Value.CustomProxyHost.Should().Be("127.0.0.1");
            config.Value.CustomProxyPort.Should().Be(80);
        }
    }
}