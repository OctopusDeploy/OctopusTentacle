using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Startup
{
    [TestFixture]
    [WindowsTest]
    [NonParallelizable]
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public class WindowsServiceConfiguratorFixture
    {
        [Test]
        public void CanInstallWindowsService()
        {
            const string serviceName = "OctopusShared.ServiceHelperTest";
            const string instance = "TestInstance";
            const string serviceDescription = "Test service for OctopusShared tests";
            var log = new InMemoryLog();
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullProcessPath());
            var exePath = Path.Combine(root, "Startup\\Packages\\Acme.Service", "Acme.Service.exe");

            DeleteExistingService(serviceName);

            var user = new TestUserPrincipal("octo-shared-svc-test");
            var serviceConfigurationState = new ServiceConfigurationState
            {
                Install = true,
                Password = user.Password,
                Username = user.NTAccountName,
                Start = true
            };
            var localAdminRightsChecker = Substitute.For<IWindowsLocalAdminRightsChecker>();
            var configureServiceHelper = new WindowsServiceConfigurator(log, Substitute.For<ILogFileOnlyLogger>(), localAdminRightsChecker);

            try
            {
                configureServiceHelper.ConfigureServiceByInstanceName(serviceName,
                    exePath,
                    instance,
                    serviceDescription,
                    serviceConfigurationState);

                using (var installedService = GetInstalledService(serviceName))
                {
                    Assert.NotNull(installedService, "Service is installed");
                    Assert.AreEqual(ServiceControllerStatus.Running, installedService.Status);
                }
                localAdminRightsChecker.Received(1).AssertIsRunningElevated();
            }
            finally
            {
                // Don't delete the user account - we don't delete the user profile, resulting in test failures when the profile names get too long
                // Security: the user account is not a member of the local admin group, and we reset the password on every execution of the test
                // user?.Delete();
                DeleteExistingService(serviceName);
            }
        }

        [Test]
        public void ThrowsOnBadServiceDependency()
        {
            const string serviceName = "OctopusShared.ServiceHelperTest";
            const string instance = "TestInstance";
            const string serviceDescription = "Test service for OctopusShared tests";
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullProcessPath());
            var exePath = Path.Combine(root, "Startup\\Packages\\Acme.Service", "Acme.Service.exe");

            DeleteExistingService(serviceName);

            var serviceConfigurationState = new ServiceConfigurationState
            {
                Install = false,
                Start = false,
                DependOn = "ServiceThatDoesNotExist"
            };
            var configureServiceHelper = new WindowsServiceConfigurator(new InMemoryLog(), Substitute.For<ILogFileOnlyLogger>(), new WindowsLocalAdminRightsChecker());

            var ex = Assert.Throws<ControlledFailureException>(() => configureServiceHelper.ConfigureServiceByInstanceName(serviceName,
                exePath,
                instance,
                serviceDescription,
                serviceConfigurationState));
            ex.Message.Should().Be("Unable to set dependency on service 'ServiceThatDoesNotExist' as no service was found with that name.");
        }
        ServiceController GetInstalledService(string serviceName)
        {
            return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName);
        }

        void DeleteExistingService(string serviceName)
        {
            var service = GetInstalledService(serviceName);
            if (service != null)
            {
                var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var sc = Path.Combine(system32, "sc.exe");

                Process.Start(new ProcessStartInfo(sc, $"stop {serviceName}") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
                Process.Start(new ProcessStartInfo(sc, $"delete {serviceName}") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
            }
        }
    }
}
