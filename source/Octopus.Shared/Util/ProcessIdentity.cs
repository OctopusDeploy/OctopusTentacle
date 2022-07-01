using System;
using System.Security.Principal;

namespace Octopus.Shared.Util
{
    public static class ProcessIdentity
    {
        public static string CurrentUserName => PlatformDetection.IsRunningOnWindows
            ?
#pragma warning disable PC001 // API not supported on all platforms
            WindowsIdentity.GetCurrent().Name
            :
#pragma warning restore PC001 // API not supported on all platforms
            Environment.UserName;
    }
}