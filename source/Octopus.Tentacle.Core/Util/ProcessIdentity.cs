using System;
using System.Security.Principal;

namespace Octopus.Tentacle.Util
{
    public static class ProcessIdentity
    {
        public static string CurrentUserName => PlatformDetection.IsRunningOnWindows
            ?
#pragma warning disable CA1416
            WindowsIdentity.GetCurrent().Name
#pragma warning restore CA1416
            :
            Environment.UserName;
    }
}