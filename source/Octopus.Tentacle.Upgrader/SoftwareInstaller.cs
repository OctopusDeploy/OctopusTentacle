using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace Octopus.Tentacle.Upgrader
{
    public class SoftwareInstaller
    {
        public int Install(string msiPath)
        {
            return InstallWithRetry(msiPath);
        }

        private static int InstallWithRetry(string msiPath)
        {
            var guid = Guid.NewGuid();
            int exitCode;
            var currentRetryCount = 0;
            var delay = TimeSpan.FromSeconds(1);
            do
            {
                var logFile = $"UpgradeLog-MsiExec-{guid}{(currentRetryCount == 0 ? "" : $"-{currentRetryCount}")}.txt";
                var args = $"/i \"{msiPath}\" /li \"{logFile}\" /quiet /norestart";

                Log.Upgrade.Info("Running MSIEXEC on MSI: " + msiPath + ", output will go to: " + logFile);

                using (var process = new Process())
                {
                    process.StartInfo.FileName = "msiexec";
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    process.WaitForExit();

                    exitCode = process.ExitCode;
                    Log.Upgrade.Info("MSIEXEC exit code was: " + exitCode);

                    if (exitCode != 0)
                    {
                        currentRetryCount++;
                        Log.Upgrade.Info($"Tentacle upgrade attempt #{currentRetryCount} faulted, retrying in {delay}");
                        Thread.Sleep(delay);
                    }
                }
            } while (exitCode != 0 && currentRetryCount < 5);

            if (exitCode != 0 && IsPendingServerRestart())
                Log.Upgrade.Info("Tentacle upgrade failed. This may be due to a pending reboot of this server.");

            return exitCode;
        }

        private static bool IsPendingServerRestart()
        {
            try
            {
                return Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager")?.GetValue("PendingFileRenameOperations") is object ||
                    Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager")?.GetValue("PendingFileRenameOperations2") is object ||
                    Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") is object;
            }
            catch
            {
                return false;
            }
        }
    }
}