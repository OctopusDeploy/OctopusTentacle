using System;
using Autofac;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Commands;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Tests.Communications
{
    public class TentacleCommunicationsModuleFixture
    {
        [Test]
        [NonParallelizable]
        public void HalibutTcpKeepAlivesShouldBeEnabledByDefault()
        {
            var container = BuildContainer();

            var halibutRuntime = container.Resolve<HalibutRuntime>();

            halibutRuntime.TimeoutsAndLimits.TcpKeepAliveEnabled.Should().BeTrue();
        }

        [Test]
        [NonParallelizable]
        public void HalibutTcpKeepAlivesCanBeDisabledWithAnEnvironmentVariable()
        {
            try
            {
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled, "False");

                var container = BuildContainer();

                var halibutRuntime = container.Resolve<HalibutRuntime>();

                halibutRuntime.TimeoutsAndLimits.TcpKeepAliveEnabled.Should().BeFalse();
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled, "");
            }
        } 
        
        [Test]
        [NonParallelizable]
        public void AnEmptyHalibutTcpKeepAlivesEnvironmentVariableIsIgnored()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled, "");

            var container = BuildContainer();

            var halibutRuntime = container.Resolve<HalibutRuntime>();

            halibutRuntime.TimeoutsAndLimits.TcpKeepAliveEnabled.Should().BeTrue();
        } 

        [Test]
        [NonParallelizable]
        public void AnInvalidHalibutTcpKeepAlivesEnvironmentVariableIsIgnored()
        {
            try
            {
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled, "NOTABOOL");

                var container = BuildContainer();

                var halibutRuntime = container.Resolve<HalibutRuntime>();

                halibutRuntime.TimeoutsAndLimits.TcpKeepAliveEnabled.Should().BeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled, "");
            }
        } 

        static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();
            var configuration = new StubTentacleConfiguration();
            configuration.TentacleCertificate = configuration.GenerateNewCertificate();
            configuration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration("NOPE"));
            builder.RegisterInstance(configuration).As<ITentacleConfiguration>();
            builder.RegisterModule<TentacleCommunicationsModule>();
            var container = builder.Build();
            return container;
        }
    }
}