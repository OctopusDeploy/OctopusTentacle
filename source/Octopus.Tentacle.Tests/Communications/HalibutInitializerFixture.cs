using System;
using System.Net;
using FluentAssertions;
using Halibut;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;

namespace Octopus.Tentacle.Tests.Communications
{
    [TestFixture]
    public class HalibutInitializerFixture
    {
        string defaultProxyHost = "127.0.0.1";
        int defaultProxyPort = 1111;
        string defaultProxyUsername = "username";
        string defaultProxyPassword = "password";
#if NET452
        [Test]
        public void UseDefaultProxyShouldUseTheDefaultWebProxy()
        {
            var proxy = BuildHalibutProxy(true, null, null, null, 0);

            proxy.Host.Should().Be(defaultProxyHost);
            proxy.Port.Should().Be(defaultProxyPort);
            //we use default network creds from credential cache though.
        }
#endif

        [Test]
        public void DoNotUseDefaultProxyAndNoCustomHostShouldSetNoProxy()
        {
            var proxy = BuildHalibutProxy(false, null, null, null, 0);

            proxy.Should().BeNull();
        }

#if DEFAULT_PROXY_IS_AVAILABLE
        [Test]
        public void SettingUsernameAndPasswordShouldSetProxyCredentials()
        {
            var proxy = BuildHalibutProxy(true, "customusername", "custompassword", null, 0);

            proxy.Password.Should().Be("custompassword");
            proxy.UserName.Should().Be("customusername");
        }
#endif
        [Test]
        public void ProvidingAHostAndPortShouldSetProxyIfUseDefaultIsFalse()
        {
            var proxy = BuildHalibutProxy(false, null, null, "127.0.0.2", 2222);

            proxy.Host.Should().Be("127.0.0.2");
            proxy.Port.Should().Be(2222);
            proxy.Password.Should().BeNull();
            proxy.UserName.Should().BeNull();
        }

        [Test]
        public void ProvidingAHostAndPortAndCredsShouldUseAllOfThem()
        {
            var proxy = BuildHalibutProxy(false, "username", "password", "127.0.0.2", 2222);

            proxy.Host.Should().Be("127.0.0.2");
            proxy.Port.Should().Be(2222);
            proxy.Password.Should().Be("password");
            proxy.UserName.Should().Be("username");
        }

        ProxyDetails BuildHalibutProxy(bool useDefaultProxy, string username, string password, string host, int port)
        {
            var config = new StubProxyConfiguration(useDefaultProxy, username, password, host, port);
            var parser = new ProxyConfigParser { GetSystemWebProxy = BuildDefaultProxy };
            return parser.ParseToHalibutProxy(config, new Uri("http://octopus.com"), Substitute.For<ISystemLog>());
        }

        WebProxy BuildDefaultProxy()
        {
            var proxy = new WebProxy
            {
                Address = new Uri($"http://{defaultProxyHost}:{defaultProxyPort}"),
                Credentials = new NetworkCredential(defaultProxyUsername, defaultProxyPassword)
            };
            return proxy;
        }

        class StubProxyConfiguration : IProxyConfiguration
        {
            public StubProxyConfiguration(bool useDefaultProxy, string customProxyUsername, string customProxyPassword, string customProxyHost, int customProxyPort)
            {
                UseDefaultProxy = useDefaultProxy;
                CustomProxyUsername = customProxyUsername;
                CustomProxyPassword = customProxyPassword;
                CustomProxyHost = customProxyHost;
                CustomProxyPort = customProxyPort;
            }

            public void Save()
            {
                throw new NotImplementedException();
            }

            public bool UseDefaultProxy { get; set; }
            public string CustomProxyUsername { get; set; }
            public string CustomProxyPassword { get; set; }
            public string CustomProxyHost { get; set; }
            public int CustomProxyPort { get; set; }
        }
    }
}
