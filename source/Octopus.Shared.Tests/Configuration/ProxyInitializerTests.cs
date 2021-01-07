using System;
using System.Net;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    [WindowsTest]
    public class ProxyInitializerTests
    {
        [Test]
        public void UseDefaultProxyShouldSetTheDefaultWebProxy()
        {
            WebRequest.DefaultWebProxy = null;

            var initializer = BuildProxyInitializer(true,
                null,
                null,
                null,
                0);

            initializer.InitializeProxy();

            Assert.NotNull(WebRequest.DefaultWebProxy);
        }

        [Test]
        public void DoNotUseDefaultProxyShouldClearTheDefaultWebProxy()
        {
            WebRequest.DefaultWebProxy = new WebProxy();

            var initializer = BuildProxyInitializer(false,
                null,
                null,
                null,
                0);

            initializer.InitializeProxy();

            Assert.Null(WebRequest.DefaultWebProxy);
        }

        [Test]
        public void SettingUsernameAndPasswordShouldSetProxyCredentials()
        {
            WebRequest.DefaultWebProxy = null;

            var initializer = BuildProxyInitializer(true,
                "username",
                "password",
                null,
                0);

            initializer.InitializeProxy();

            var credentials = (NetworkCredential)WebRequest.DefaultWebProxy.Credentials;

            Assert.AreEqual("password", credentials.Password);
            Assert.AreEqual("username", credentials.UserName);
        }

        [Test]
        public void ProvidingAHostAndPortShouldSetProxyIfUseDefaultIsFalse()
        {
            WebRequest.DefaultWebProxy = null;

            var initializer = BuildProxyInitializer(false,
                null,
                null,
                "127.0.0.1",
                8888);

            initializer.InitializeProxy();

            var proxyUri = WebRequest.DefaultWebProxy.GetProxy(new Uri("http://google.com"));

            Assert.AreEqual("127.0.0.1", proxyUri.Host);
            Assert.AreEqual(8888, proxyUri.Port);
        }

        [Test]
        public void NoCredentialsWithACustomProxySetsCredentialsToEmpty()
        {
            WebRequest.DefaultWebProxy = null;

            var initializer = BuildProxyInitializer(false,
                null,
                null,
                "127.0.0.1",
                8888);

            initializer.InitializeProxy();

            var credentials = (NetworkCredential)WebRequest.DefaultWebProxy.Credentials;

            Assert.AreEqual("", credentials.Password);
            Assert.AreEqual("", credentials.UserName);
        }

        ProxyInitializer BuildProxyInitializer(bool useDefaultProxy,
            string username,
            string password,
            string host,
            int port)
        {
            var config = new StubProxyConfiguration(useDefaultProxy,
                username,
                password,
                host,
                port);
            var parser = new ProxyConfigParser();
            return new ProxyInitializer(config, parser, Substitute.For<ISystemLog>());
        }

        class StubProxyConfiguration : IProxyConfiguration
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

            public void Save()
            {
                throw new NotImplementedException();
            }
        }
    }
}