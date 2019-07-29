using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Octopus.Shared.Startup;
using Octopus.Shared.Tests.Support;
using Octopus.Shared.Tests.Util;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Startup
{
    [TestFixture]
    public class ConfigureSystemdHelperFixture
    {
        [Test]
        public void CanInstallService()
        {
            if(PlatformDetection.IsRunningOnWindows)
                Assert.Inconclusive("This test is only supported on Linux.");
        
            const string serviceName = "OctopusShared.ServiceHelperTest";
            const string instance = "TestInstance";
            const string serviceDescription = "Test service for OctopusShared tests";
            var log = new InMemoryLog();
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullLocalPath());
            var assemblyPath = Path.Combine(root, "Startup/Packages/Acme.SampleConsole", "Acme.SampleConsole.dll");
            var targetPath = $"dotnet {assemblyPath}";

            var configureServiceHelper = new LinuxServiceConfigurator(log);
            
            var serviceConfigurationState = new ServiceConfigurationState
            {
                Install = true,
                Start = true
            };
            
            configureServiceHelper.ConfigureService(serviceName, targetPath, instance, serviceDescription, serviceConfigurationState);
            
            //Check that the systemd unit service file has been written
            Assert.IsTrue(DoesServiceUnitFileExist(instance), "The service unit file has not been created");
            
            //Check that the Service is running
            Assert.IsTrue(IsServiceRunning(instance), "The service is not running");
                
            //Check that the service is enabled to run on startup
            Assert.IsTrue(IsServiceEnabled(instance), "The service has not been enabled to run on startup");
            
            var stopServiceConfigurationState = new ServiceConfigurationState
            {
                Stop = true,
                Uninstall = true
            };
            
            configureServiceHelper.ConfigureService(serviceName, targetPath, instance, serviceDescription, stopServiceConfigurationState);
            
            //Check that the Service has stopped
            Assert.IsFalse(IsServiceRunning(instance), "The service has not been stopped");
                
            //Check that the service is disabled
            Assert.IsFalse(IsServiceEnabled(instance), "The service has not been disabled");
            
            //Check that the service is disabled
            Assert.IsFalse(DoesServiceUnitFileExist(instance), "The service unit file still exists");
        }

        private bool IsServiceRunning(string serviceName)
        {
            var result = RunBashCommand($"systemctl is-active --quiet {serviceName}");
            return result.ExitCode == 0;
        }
        
        private bool IsServiceEnabled(string serviceName)
        {
            var result = RunBashCommand($"systemctl is-enabled --quiet {serviceName}");
            return result.ExitCode == 0;
        }

        private bool DoesServiceUnitFileExist(string serviceName)
        {
            var result = RunBashCommand($"ls /etc/systemd/system | grep {serviceName}.service");
            return result.ExitCode == 0;
        }

        private CmdResult RunBashCommand(string command)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"{command}\"");
            return commandLineInvocation.ExecuteCommand();
        }
    }
}