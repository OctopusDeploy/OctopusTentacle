using System.Security.Principal;
using System;
using System.Linq;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TestEnvironmentHelper
    {
#pragma warning disable CA1416
        public static string CurrentFullUserName => PlatformDetection.IsRunningOnWindows ? WindowsIdentity.GetCurrent().Name : Environment.UserName;
        public static string CurrentUserName => PlatformDetection.IsRunningOnWindows ? WindowsIdentity.GetCurrent().Name.Split('\\').Last() : Environment.UserName;
#pragma warning restore CA1416
    }
}