using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    public interface ICommandHostStrategy
    {
        public ICommandHost SelectMostAppropriateHost(ICommand command,
            string displayName,
            ISystemLog log,
            bool forceConsoleHost,
            bool forceNoninteractiveHost,
            string? monitorMutexHost);
    }
    
    public class CommandHostStrategy : ICommandHostStrategy
    {
        public ICommandHost SelectMostAppropriateHost(ICommand command,
            string displayName,
            ISystemLog log,
            bool forceConsoleHost,
            bool forceNoninteractiveHost,
            string? monitorMutexHost)
        {
            log.Trace("Selecting the most appropriate host");

            var commandSupportsConsoleSwitch = ConsoleHost.HasConsoleSwitch(command.Options);

            if (monitorMutexHost != null && !string.IsNullOrEmpty(monitorMutexHost))
            {
                log.Trace("The --monitorMutex switch was provided for a supported command");
                return new MutexHost(monitorMutexHost, log);
            }

            if (forceNoninteractiveHost && commandSupportsConsoleSwitch)
            {
                log.Trace("The --noninteractive switch was provided for a supported command");
                return new NoninteractiveHost();
            }

            if (!command.CanRunAsService)
            {
                log.Trace($"The {command.GetType().Name} must run interactively; using a console host");
                return new ConsoleHost(displayName);
            }

            if (forceConsoleHost && commandSupportsConsoleSwitch)
            {
                log.Trace($"The {ConsoleHost.ConsoleSwitchExample} switch was provided for a supported command, must run interactively; using a console host");
                return new ConsoleHost(displayName);
            }

            if (IsRunningAsAWindowsService(log))
            {
                log.Trace("The program is not running interactively; using a Windows Service host");
                return new WindowsServiceHost(log);
            }

            log.Trace("The program is running interactively; using a console host");
            return new ConsoleHost(displayName);
        }

        static bool IsRunningAsAWindowsService(ISystemLog log)
        {
            if (PlatformDetection.IsRunningOnMac || PlatformDetection.IsRunningOnNix)
                return false;

#if USER_INTERACTIVE_DOES_NOT_WORK
            try
            {
                var child = Process.GetCurrentProcess();

                var parentPid = 0;

                var hnd = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);

                if (hnd == IntPtr.Zero)
                    return false;

                var processInfo = new Kernel32.PROCESSENTRY32
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(Kernel32.PROCESSENTRY32))
                };

                if (Kernel32.Process32First(hnd, ref processInfo) == false)
                    return false;

                do
                {
                    if (child.Id == processInfo.th32ProcessID)
                        parentPid = (int)processInfo.th32ParentProcessID;
                } while (parentPid == 0 && Kernel32.Process32Next(hnd, ref processInfo));

                if (parentPid <= 0)
                    return false;

                var parent = Process.GetProcessById(parentPid);
                return parent.ProcessName.ToLower() == "services";
            }
            catch (Exception ex)
            {
                log.Trace(ex, "Could not determine whether the parent process was the service host, assuming it isn't");
                return false;
            }
#else
            return !Environment.UserInteractive;
#endif
        }
#pragma warning disable PC003 // Native API not available in UWP
#if USER_INTERACTIVE_DOES_NOT_WORK
        static class Kernel32
        {
            public static readonly uint TH32CS_SNAPPROCESS = 2;

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

            [DllImport("kernel32.dll")]
            public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            [DllImport("kernel32.dll")]
            public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESSENTRY32
            {
                public uint dwSize;
                public readonly uint cntUsage;
                public readonly uint th32ProcessID;
                public readonly IntPtr th32DefaultHeapID;
                public readonly uint th32ModuleID;
                public readonly uint cntThreads;
                public readonly uint th32ParentProcessID;
                public readonly int pcPriClassBase;
                public readonly uint dwFlags;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public readonly string szExeFile;
            }
        }
#endif
#pragma warning restore PC003 // Native API not available in UWP
    }
}
