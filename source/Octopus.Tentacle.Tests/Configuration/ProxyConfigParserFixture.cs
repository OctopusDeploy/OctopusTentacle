using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Tests.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Configuration
{
    public class ProxyConfigParserFixture
    {
        private const string Destination = "https://localhost";

        [Test]
        [WindowsTest]
        public void ShouldParseToHalibutProxyOnWindows()
        {
            var log = new InMemoryLog();
            var parser = new ProxyConfigParser();
            var config = new StubProxyConfiguration(true,
                null,
                null,
                null,
                0);
            var result = parser.ParseToHalibutProxy(config, new Uri(Destination), log);
#if DEFAULT_PROXY_IS_NOT_AVAILABLE
            result.Should().BeNull();
#else
            log.GetLog()
                .Should()
                .Contain(string.Format(ProxyConfigParser.ProxyNotConfiguredForDestination, Destination));
#endif
        }

        [Test]
        [LinuxTest]
        public void ShouldParseToHalibutProxyOnLinux()
        {
            var parser = new ProxyConfigParser();
            var config = new StubProxyConfiguration(true,
                null,
                null,
                null,
                0);
            var result = parser.ParseToHalibutProxy(config, new Uri("https://localhost"), new InMemoryLog());
            result.Should().BeNull();
        }

        private class StubProxyConfiguration : IProxyConfiguration
        {
            public StubProxyConfiguration(bool useDefaultProxy,
                string customProxyUsername,
                string customProxyPassword,
                string customProxyHost,
                int customProxyPort)
            {
                UseDefaultProxy = useDefaultProxy;
                CustomProxyUsername = customProxyUsername;
                CustomProxyPassword = customProxyPassword;
                CustomProxyHost = customProxyHost;
                CustomProxyPort = customProxyPort;
            }

            public bool UseDefaultProxy { get; }
            public string CustomProxyUsername { get; }
            public string CustomProxyPassword { get; }
            public string CustomProxyHost { get; }
            public int CustomProxyPort { get; }
        }
    }
}