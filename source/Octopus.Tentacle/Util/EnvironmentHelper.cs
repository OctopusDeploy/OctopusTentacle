using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Octopus.Tentacle.Util
{
    public static class EnvironmentHelper
    {
#pragma warning disable PC001 // API not supported on all platforms
        private static string CurrentUserName => PlatformDetection.IsRunningOnWindows ? WindowsIdentity.GetCurrent().Name : Environment.UserName;
#pragma warning restore PC001 // API not supported on all platforms
        public static string[] SafelyGetEnvironmentInformation()
        {
            var envVars = GetEnvironmentVars()
                .Concat(GetPathVars())
                .Concat(GetProcessVars());
            return envVars.ToArray();
        }

        private static string SafelyGet(Func<string> thingToGet)
        {
            try
            {
                return thingToGet.Invoke();
            }
            catch (Exception)
            {
                return "Unable to retrieve environment information.";
            }
        }

        private static IEnumerable<string> GetEnvironmentVars()
        {
            yield return SafelyGet(() => $"OperatingSystem: {RuntimeInformation.OSDescription}");
            yield return SafelyGet(() => $"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
            yield return SafelyGet(() => $"Is64BitProcess: {Environment.Is64BitProcess}");
            yield return SafelyGet(() => $"CurrentUser: {CurrentUserName}");
            yield return SafelyGet(() => $"MachineName: {Environment.MachineName}");
            yield return SafelyGet(() => $"ProcessorCount: {Environment.ProcessorCount}");
        }

        private static IEnumerable<string> GetPathVars()
        {
            yield return SafelyGet(() => $"CurrentDirectory: {Directory.GetCurrentDirectory()}");
            yield return SafelyGet(() => $"TempDirectory: {Path.GetTempPath()}");
        }

        private static IEnumerable<string> GetProcessVars()
        {
            yield return SafelyGet(() => $"HostProcessName: {Process.GetCurrentProcess().ProcessName}");
            yield return SafelyGet(() => $"PID: {Process.GetCurrentProcess().Id}");
        }
    }
}