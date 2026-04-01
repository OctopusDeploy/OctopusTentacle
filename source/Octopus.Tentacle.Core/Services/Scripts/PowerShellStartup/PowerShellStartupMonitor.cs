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
        /// Deletes the should-run file so that if PowerShell does eventually start, the startup guard
        /// will detect its absence and exit immediately.
        ///
        /// Without this, a race exists: if the workspace is cleaned up while PowerShell is blocked,
        /// the started sentinel file disappears. The guard would then be able to create it successfully
        /// and incorrectly conclude that it is safe to proceed. Deleting the should-run file closes
        /// that gap — the guard always checks for it and exits with code -47 if it is missing.
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
