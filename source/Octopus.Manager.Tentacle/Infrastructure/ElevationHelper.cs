using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class ElevationHelper
    {
        public static bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

        public static void Elevate(IEnumerable<string> args)
        {
            try
            {
                // We removed this class in favour of using the application manifest for elevation... but then!
                // See https://github.com/OctopusDeploy/Issues/issues/3875
                var info = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, String.Join(" ", args));
                info.Verb = "runas";

                var process = new Process {StartInfo = info};

                process.Start();
            }
            catch (Exception e)
            {
                throw new Exception("Failed to restart as an elevated process. The Octopus Tentacle Manager requires elevated privileges on this server. Please check your user account is a member of the Administrator group on this server, and answer 'YES' to any Windows UAC prompts.", e);
            }
        }
    }
}