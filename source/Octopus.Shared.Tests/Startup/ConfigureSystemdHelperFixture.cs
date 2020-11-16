using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Octopus.Shared.Startup;
using Octopus.Shared.Tests.Support;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Startup
{
    [TestFixture]
    [LinuxTest]
    public class ConfigureSystemdHelperFixture
    {
        [Test]
        public void CanInstallService()
        {
            const string serviceName = "OctopusShared.ServiceHelperTest";
            const string instance = "TestInstance";
            const string serviceDescription = "Test service for OctopusShared tests";
            var log = new InMemoryLog();
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullProcessPath());
            var scriptPath = Path.Combine(root, "SampleScript.sh");
            WriteUnixFile(scriptPath);

            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"chmod 777 {scriptPath}\"");
            commandLineInvocation.ExecuteCommand();

            var configureServiceHelper = new LinuxServiceConfigurator(log);

            var serviceConfigurationState = new ServiceConfigurationState
            {
                Install = true,
                Start = true
            };

            configureServiceHelper.ConfigureService(serviceName,
                scriptPath,
                instance,
                serviceDescription,
                serviceConfigurationState);

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

            configureServiceHelper.ConfigureService(serviceName,
                scriptPath,
                instance,
                serviceDescription,
                stopServiceConfigurationState);

            //Check that the Service has stopped
            Assert.IsFalse(IsServiceRunning(instance), "The service has not been stopped");

            //Check that the service is disabled
            Assert.IsFalse(IsServiceEnabled(instance), "The service has not been disabled");

            //Check that the service is disabled
            Assert.IsFalse(DoesServiceUnitFileExist(instance), "The service unit file still exists");
        }

        void WriteUnixFile(string path)
        {
            using (TextWriter writer = new StreamWriter(path, false, Encoding.ASCII))
            {
                writer.NewLine = "\n";
                writer.WriteLine("#!/bin/bash");
                writer.WriteLine("");
                writer.WriteLine("while true; do now=$(date +\"%T\");echo \"Current time : $now\";sleep 1; done\n");
                writer.Close();
            }
        }

        bool IsServiceRunning(string serviceName)
        {
            var result = RunBashCommand($"systemctl is-active --quiet {serviceName}");
            return result.ExitCode == 0;
        }

        bool IsServiceEnabled(string serviceName)
        {
            var result = RunBashCommand($"systemctl is-enabled --quiet {serviceName}");
            return result.ExitCode == 0;
        }

        bool DoesServiceUnitFileExist(string serviceName)
        {
            var result = RunBashCommand($"ls /etc/systemd/system | grep {serviceName}.service");
            return result.ExitCode == 0;
        }

        CmdResult RunBashCommand(string command)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"{command}\"");
            return commandLineInvocation.ExecuteCommand();
        }
    }
}