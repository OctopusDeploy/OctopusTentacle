using System;
using System.Security.Principal;

namespace Octopus.Shared.Util
{
    public static class ProcessIdentity
    {
        public static string CurrentUserName => PlatformDetection.IsRunningOnWindows ?
            WindowsIdentity.GetCurrent().Name :
            Environment.UserName;
    }
}