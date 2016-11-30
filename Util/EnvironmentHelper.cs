using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Octopus.Shared.Util
{
    public static class EnvironmentHelper
    {
        public static string[] SafelyGetEnvironmentInformation()
        {
            var envVars = SafelyGetEnvironmentVars()
                .Concat(SafelyGetPathVars())
                .Concat(SafelyGetProcessVars())
                .Concat(SafelyGetComputerInfoVars());
            return envVars.ToArray();
        }

        static IEnumerable<string> SafelyGetEnvironmentVars()
        {
            try
            {
                return GetEnvironmentVars();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetEnvironmentVars()
        {
            yield return $"OperatingSystem: {Environment.OSVersion.ToString()}";
            yield return $"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}";
            yield return $"Is64BitProcess: {Environment.Is64BitProcess.ToString()}";
            yield return $"CurrentUser: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}";
            yield return $"MachineName: {Environment.MachineName}";
            yield return $"ProcessorCount: {Environment.ProcessorCount.ToString()}";
        }

        static IEnumerable<string> SafelyGetPathVars()
        {
            try
            {
                return GetPathVars();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetPathVars()
        {
            yield return $"CurrentDirectory: {Directory.GetCurrentDirectory()}";
            yield return $"TempDirectory: {Path.GetTempPath()}";
        }

        static IEnumerable<string> SafelyGetProcessVars()
        {
            try
            {
                return GetProcessVars();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetProcessVars()
        {
            yield return $"HostProcessName: {Process.GetCurrentProcess().ProcessName}";
        }

        static IEnumerable<string> SafelyGetComputerInfoVars()
        {
            try
            {
                return Enumerable.Empty<string>();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetComputerInfoVars()
        {
            var computerInfo = new ComputerInfo();
            yield return $"TotalPhysicalMemory: {computerInfo.TotalPhysicalMemory.ToFileSizeString()}";
            yield return $"AvailablePhysicalMemory: {computerInfo.AvailablePhysicalMemory.ToFileSizeString()}";
        }
    }
}
