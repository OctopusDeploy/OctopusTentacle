using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Core.Services.Scripts.PowerShellStartup
{
    class PowerShellStartupMonitor
    {
        readonly string workSpaceWorkingDirectory;
        readonly TimeSpan powerShellStartupTimeout;
        readonly ILog log;
        readonly string taskId;

        public PowerShellStartupMonitor(string workSpaceWorkingDirectory, TimeSpan powerShellStartupTimeout, ILog log, string taskId)
        {
            this.workSpaceWorkingDirectory = workSpaceWorkingDirectory;
            this.powerShellStartupTimeout = powerShellStartupTimeout;
            this.log = log;
            this.taskId = taskId;
        }

        public Task<PowerShellStartupStatus> WaitForStartup(CancellationToken cancellationToken)
        {
            var shouldRunFilePath = PowerShellStartupDetection.GetShouldRunFilePath(workSpaceWorkingDirectory);

            if (!File.Exists(shouldRunFilePath))
            {
                return Task.FromResult(PowerShellStartupStatus.NotMonitored);
            }

            return Task.Run(async () =>
            {
                try
                {
                    await DelayWithoutException.Delay(powerShellStartupTimeout, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return PowerShellStartupStatus.NotMonitored;
                    }

                    try
                    {
                        var startedFilePath = PowerShellStartupDetection.GetStartedFilePath(workSpaceWorkingDirectory);
                        // If we can make the file, then the script has not started.
                        using var fileStream = File.Open(startedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        log.Warn($"PowerShell startup detection: PowerShell did not start within {powerShellStartupTimeout.TotalMinutes} minutes for task {taskId}");
                        DeleteShouldRunFileToEnsureThePowerShellCanNeverStart();
                        return PowerShellStartupStatus.NeverStarted;
                    }
                    catch (IOException)
                    {
                        log.Info($"PowerShell startup detection: PowerShell started late (after {powerShellStartupTimeout.TotalMinutes} minutes) for task {taskId}");
                        return PowerShellStartupStatus.Started;
                    }
                }
                catch (OperationCanceledException)
                {
                    return PowerShellStartupStatus.NotMonitored;
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"Error in PowerShell startup monitoring for task {taskId}");
                    return PowerShellStartupStatus.NotMonitored;
                }
            });
        }

        /// <summary>
        /// Since the powershell guard works by only running if it can create a file, we run into an
        /// interesting situation when the powershell is blocked from running and the workspace is
        /// cleaned up. If the workspace is cleaned up, then the file that the guard must create in
        /// order to processed wont exist. This means it could proceed with execution. To ensure this
        /// never happens, we delete the should-run file. Since the guard will only run if this file
        /// exists, the workspace deleting problem goes away. 
        /// </summary>
        public void DeleteShouldRunFileToEnsureThePowerShellCanNeverStart()
        {
            try
            {
                var shouldRunFilePath = PowerShellStartupDetection.GetShouldRunFilePath(workSpaceWorkingDirectory);
                if (File.Exists(shouldRunFilePath))
                {
                    File.Delete(shouldRunFilePath);
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex, $"Failed to delete should-run file for task {taskId}");
            }
        }
    }
}
