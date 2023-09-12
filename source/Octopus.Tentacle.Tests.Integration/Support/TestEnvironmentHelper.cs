using System.Security.Principal;
using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TestEnvironmentHelper
    {
#pragma warning disable CA1416
        public static string CurrentUserName => PlatformDetection.IsRunningOnWindows ? WindowsIdentity.GetCurrent().Name : Environment.UserName;
#pragma warning restore CA1416

        // These "Environment" versions are required for testing older operating systems on TeamCity, as "CurrentUserName" gives different values to what is actually set in the environment...
        public static string EnvironmentUserName => Environment.GetEnvironmentVariable("username") ?? throw new Exception("Environment variable 'username' not set");
        public static string EnvironmentDomain => Environment.GetEnvironmentVariable("userdomain") ?? throw new Exception("Environment variable 'userdomain' not set");
    }
}