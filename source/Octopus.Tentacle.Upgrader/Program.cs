#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Octopus.Tentacle.Upgrader
{
    class Program
    {
        static readonly string Version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        static int Main(string[] args)
        {
            //we log this as early as possible as a canary to make sure the upgrader launches
            //if the upgrade log file doesn't exist, we assume that we couldn't launch the upgrader
            //for some reason (ie, net framework not installed type issue)
            var appName = typeof(Program).Assembly.GetName().Name;
            Log.Upgrade.Info($"{appName} {Version} started...");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.ExitCode.Info("This only works on Windows OS");
                return -1;
            }

            var exitCode = PerformUpgrade(args);

            Log.ExitCode.Info(exitCode.ToString());

            return exitCode;
        }

        static int PerformUpgrade(string[] args)
        {
            LogStartupParameters(args);
            if (args.Length < 3)
            {
                Log.Upgrade.Info("Error: Invalid arguments");
                return 1;
            }

            var semaphore = new Semaphore(1, 1, "OctopusDeploy.UpgradeSemaphore");

            if (!semaphore.WaitOne(TimeSpan.FromMilliseconds(100)))
            {
                Log.Upgrade.Info("Warning: Another upgrader is already running. Exiting on the assumption that the other process will take care of the upgrade.");
                return 0;
            }

            Log.Upgrade.Info("Upgrade mutex acquired");

            Thread.Sleep(3000);

            var serviceBouncer = new ServiceBouncer(args[0]);
            try
            {
                Log.Upgrade.Info("Stopping Tentacle services");
                serviceBouncer.StopAll();

                Log.Upgrade.Info("Starting MSI installer");
                var msi = SelectAppropriateMsi(args);
                var installer = new SoftwareInstaller();
                return installer.Install(msi);
            }
            catch (Exception ex)
            {
                Log.Upgrade.Info("Error: " + ex);
                return -1;
            }
            finally
            {
                semaphore.Release();
                serviceBouncer.StartAnyThatWerePreviouslyStarted();
            }
        }

        static void LogStartupParameters(IList<string> args)
        {
            Log.Upgrade.Info("Octopus upgrader version " + typeof(Program).Assembly.GetName().Version);
            Log.Upgrade.Info("Current directory: " + Environment.CurrentDirectory);
            Log.Upgrade.Info("Arguments: ");
            for (var i = 0; i < args.Count; i++)
            {
                Log.Upgrade.Info(" [" + i + "] = \"" + args[i] + "\"");
            }
        }

        static string SelectAppropriateMsi(IList<string> args)
        {
            var msi = Environment.Is64BitOperatingSystem ? args[2] : args[1];
            if (!File.Exists(msi))
                throw new FileNotFoundException("Expected to find an MSI at " + msi);
            return msi;
        }
    }
}
