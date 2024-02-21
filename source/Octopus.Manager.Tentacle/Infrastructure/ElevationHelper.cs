using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class ElevationHelper
    {
#pragma warning disable CA1416
        public static bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416

        public static void Elevate(IEnumerable<string> args)
        {
            try
            {
                // We removed this class in favour of using the application manifest for elevation... but then!
                // See https://github.com/OctopusDeploy/Issues/issues/3875
                //var fileName = Assembly.GetEntryAssembly().Location;
                using (var currentProcess = Process.GetCurrentProcess())
                {
                    var fileName = currentProcess.MainModule.FileName;
                    var info = new ProcessStartInfo(fileName, String.Join(" ", args))
                    {
                        Verb = "runas",
                        UseShellExecute = true
                    };

                    var process = new Process {StartInfo = info};

                    process.Start();
                }
            }
            catch (Exception e)
            {
                throw new Exception("Failed to restart as an elevated process. The Octopus Tentacle Manager requires elevated privileges on this server. Please check your user account is a member of the Administrator group on this server, and answer 'YES' to any Windows UAC prompts.", e);
            }
        }
    }
}
