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
            var info = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, String.Join(" ", args));
            info.Verb = "runas";

            var process = new Process {StartInfo = info};
            process.Start();
        }
    }
}