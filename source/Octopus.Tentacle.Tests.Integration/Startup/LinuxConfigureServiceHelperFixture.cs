using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Startup
{
    [TestFixture]
    [LinuxTest]
    [NonParallelizable]
    public class LinuxConfigureServiceHelperFixture
    {
        [Test]
        [RequiresSudoOnLinux]
        public void CanInstallServiceAsRoot()
        {
            CanInstallService(null, null);
        }

        [Test]
        [RequiresSudoOnLinux]
        public void CanInstallServiceAsUser()
        {
            var user = new LinuxTestUserPrincipal("octo-shared-svc-test");
            CanInstallService(user.UserName, user.Password);
        }

        [Test]
        [RequiresSudoOnLinux]
        public void CannotWriteToServiceFileAsUser()
        {
            const string serviceName = "OctopusShared.ServiceHelperTest";
            const string instance = "TestCannotWriteToServiceFileInstance";
            const string serviceDescription = "Test service for OctopusShared tests";
            var log = new InMemoryLog();
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullProcessPath());
            var scriptPath = Path.Combine(root, "SampleScript.sh");
            WriteUnixFile(scriptPath);

            var chmodCmd = new CommandLineInvocation("/bin/bash", $"-c \"chmod 777 {scriptPath}\"");
            chmodCmd.ExecuteCommand();

            var configureServiceHelper = new LinuxServiceConfigurator(log);

            var serviceConfigurationState = new ServiceConfigurationState
            {
                Install = true,
                Start = true,
                Username = "user",
                Password = "password"
            };

            configureServiceHelper.ConfigureServiceByInstanceName(serviceName,
                scriptPath,
                instance,
                serviceDescription,
                serviceConfigurationState);

            var statCmd = new CommandLineInvocation("/bin/bash", $"-c \"stat -c '%A' /etc/systemd/system/{instance}.service\"");
            var result = statCmd.ExecuteCommand();
            result.Infos.Single().Should().Be("-rw-r--r--"); // Service file should only be writeable for the root user
        }

        void CanInstallService(string username, string password)
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
                Start = true,
                Username = username,
                Password = password
            };

            configureServiceHelper.ConfigureServiceByInstanceName(serviceName,
                scriptPath,
                instance,
                serviceDescription,
                serviceConfigurationState);

            //Check that the systemd unit service file has been written
            Assert.IsTrue(DoesServiceUnitFileExist(instance), "The service unit file has not been created");

            var status = GetServiceStatus(instance);
            status["ActiveState"].Should().Be("active");
            status["SubState"].Should().Be("running");
            status["LoadState"].Should().Be("loaded");
            status["User"].Should().Be(username ?? "root");

            //Check that the Service is running
            Assert.IsTrue(IsServiceRunning(instance), "The service is not running");

            //Check that the service is enabled to run on startup
            Assert.IsTrue(IsServiceEnabled(instance), "The service has not been enabled to run on startup");

            var stopServiceConfigurationState = new ServiceConfigurationState
            {
                Stop = true,
                Uninstall = true
            };

            configureServiceHelper.ConfigureServiceByInstanceName(serviceName,
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

        Dictionary<string, string> GetServiceStatus(string serviceName)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"systemctl show {serviceName}\"");
            var result = commandLineInvocation.ExecuteCommand();
            Console.WriteLine($"Status of service {serviceName}");
            foreach (var info in result.Infos)
                Console.WriteLine(info);
            return result.Infos
                .Select(x => x.Split(new[] { '=' }, 2, StringSplitOptions.None))
                .ToDictionary(x => x[0], x => x[1]);
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
