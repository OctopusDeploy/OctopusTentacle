using System;
using System.Security.Principal;

namespace Octopus.Tentacle.Util
{
    public interface IWindowsLocalAdminRightsChecker
    {
        public void AssertIsRunningElevated(string message = "This command requires elevation.");
        public bool IsRunningElevated();
    }

    public class WindowsLocalAdminRightsChecker : IWindowsLocalAdminRightsChecker
    {
        public void AssertIsRunningElevated(string message = "This command requires elevation.")
        {
            if (!PlatformDetection.IsRunningOnWindows)
                throw new NotSupportedException("This class only checks for admin rights on windows machines");

            if (!IsRunningElevated())
                throw new ControlledFailureException($"{message} Please re-run from an elevated shell / as a user that is a member of the local administrators group.");
        }

        public bool IsRunningElevated()
        {
            if (!PlatformDetection.IsRunningOnWindows)
                throw new NotSupportedException("This class only checks for admin rights on windows machines");

#pragma warning disable PC001 // API not supported on all platforms
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
#pragma warning restore PC001 // API not supported on all platforms
        }
    }
}