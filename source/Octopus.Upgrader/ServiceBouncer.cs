#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace Octopus.Upgrader
{
    public class ServiceBouncer
    {
        readonly string servicePrefix;
        readonly HashSet<string> servicesToStart = new HashSet<string>();

        public ServiceBouncer(string servicePrefix)
        {
            this.servicePrefix = servicePrefix;
        }

        public void StopAll()
        {
            var services = ServiceController.GetServices();
            foreach (var service in services.Where(IsOurService))
            {
                if (service.Status == ServiceControllerStatus.Running)
                    servicesToStart.Add(service.ServiceName);

                StopService(service);
            }
        }

        static void StopService(ServiceController service)
        {
            Log.Upgrade.Info("Stopping service: " + service.ServiceName);
            if (service.Status != ServiceControllerStatus.Stopped && service.Status != ServiceControllerStatus.StopPending)
            {
                while (!service.CanStop)
                {
                    service.Refresh();
                    Log.Upgrade.Info("Waiting for the service to be ready to stop...");
                    Thread.Sleep(300);
                }

                Log.Upgrade.Info("Stopping service...");
                Thread.Sleep(1000);
                service.Stop();
            }

            while (service.Status != ServiceControllerStatus.Stopped)
            {
                service.Refresh();

                Log.Upgrade.Info("Waiting for service to stop. Current status: " + service.Status);
                Thread.Sleep(300);
            }

            Log.Upgrade.Info("Service stopped");
        }

        public void StartAnyThatWerePreviouslyStarted()
        {
            var exceptionsEncountered = new List<Exception>();

            var services = ServiceController.GetServices();
            foreach (var service in services.Where(service => servicesToStart.Contains(service.ServiceName)))
            {
                try
                {
                    EnsureServiceExecutablePathIsCorrect(service);
                    StartService(service);
                }
                catch (Exception ex)
                {
                    exceptionsEncountered.Add(ex);
                }
            }

            if (exceptionsEncountered.Any())
            {
                throw new AggregateException(exceptionsEncountered);
            }
        }

        void EnsureServiceExecutablePathIsCorrect(ServiceController service)
        {
            var serviceImagePath = GetRegistryValue($@"SYSTEM\CurrentControlSet\Services\{service.ServiceName}", "ImagePath");
            var tentacleInstallLocation = GetRegistryValue(@"SOFTWARE\Octopus\Tentacle", "InstallLocation") ?? $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\Octopus Deploy\Tentacle";
            var tentacleExePath = Path.Combine(tentacleInstallLocation, "Tentacle.exe");

            var newImagePath = $"\"{tentacleExePath}\" run --instance=\"{GetInstanceName(service)}\"";
            if (newImagePath.Equals(serviceImagePath)) return;

            Log.Upgrade.Info($"Updating executable path of {service.ServiceName}, from {serviceImagePath} to {newImagePath}");

            var arguments = string.Join(" ", "config", $"\"{service.ServiceName}\"", "binpath=", $"\"\\\"{tentacleExePath}\\\" run --instance=\\\"{GetInstanceName(service)}\\\"\"");

            Log.Upgrade.Info("Running SC.exe " + arguments);
            using (var process = new Process())
            {
                process.StartInfo.FileName = "sc.exe";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                process.WaitForExit();

                Log.Upgrade.Info("SC.exe exit code was: " + process.ExitCode);
            }
        }

        string? GetRegistryValue(string registryPath, string value)
        {
            var hklm = Registry.LocalMachine;
            var key = hklm.OpenSubKey(registryPath);
            var regValue = key?.GetValue(value);
            if (key == null || regValue == null)
                return null;

            var keyValue = Environment.ExpandEnvironmentVariables(regValue.ToString()!);
            key.Close();

            return keyValue;
        }

        static void StartService(ServiceController service)
        {
            Log.Upgrade.Info("Starting: " + service.ServiceName);

            try
            {
                if (service.Status != ServiceControllerStatus.Running && service.Status != ServiceControllerStatus.StartPending)
                {
                    service.Start();
                }

                while (service.Status != ServiceControllerStatus.Running)
                {
                    service.Refresh();

                    Log.Upgrade.Info("Waiting for service to start. Current status: " + service.Status);
                    Thread.Sleep(300);
                }

                Log.Upgrade.Info("Service started");
            }
            catch (Exception ex)
            {
                Log.Upgrade.Info("Unable to start: " + ex);
                throw;
            }
        }

        bool IsOurService(ServiceController service)
        {
            return string.Equals(service.ServiceName, servicePrefix, StringComparison.OrdinalIgnoreCase)
                || service.ServiceName.StartsWith(servicePrefix + ":", StringComparison.OrdinalIgnoreCase);
        }

        string GetInstanceName(ServiceController service)
        {
            return string.Equals(service.ServiceName, servicePrefix, StringComparison.OrdinalIgnoreCase)
                ? "Tentacle"
                : service.ServiceName.Replace($"{servicePrefix}: ", "");
        }
    }
}