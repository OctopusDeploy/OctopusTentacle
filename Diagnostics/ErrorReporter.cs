using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Principal;
using Mindscape.Raygun4Net;
using Mindscape.Raygun4Net.Messages;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Configuration;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Diagnostics
{
    class ErrorReporter : IErrorReporter
    {
        readonly IUpgradeCheckConfiguration upgradeCheck;
        readonly ILog log;
        readonly RaygunClient client = new RaygunClient("89i0OHHm5GeDKb6FUANjyA==");

        public ErrorReporter(IUpgradeCheckConfiguration upgradeCheck, ILog log)
        {
            this.upgradeCheck = upgradeCheck;
            this.log = log;
        }

        public void ReportError(Exception exception)
        {
            if (!upgradeCheck.ReportErrorsOnline)
                return;

            try
            {
                var customData = new Dictionary<string, string>();
                customData["Application"] = Assembly.GetEntryAssembly().GetName().Name;
                customData["64-bit OS"] = Environment.Is64BitOperatingSystem.ToString();
                customData["64-bit process"] = Environment.Is64BitProcess.ToString();
                customData["CLR version"] = Environment.Version.ToString();
                customData["Running interactively"] = Environment.UserInteractive.ToString();
                customData["User is local administrator"] = GetAdminStatus();
                customData["Is in AD domain"] = IsJoinedToDomain().ToString();
                customData["FIPS enabled"] = ReadFipsConfigValue().ToString();

                var message = RaygunMessageBuilder.New
                    .SetExceptionDetails(exception)
                    .SetEnvironmentDetails()
                    .SetMachineName("Not provided")
                    .SetUserCustomData(customData)
                    .Build();

                message.Details.Tags = new List<string>();
                message.Details.Tags.Add(Assembly.GetEntryAssembly().GetName().Name);
                message.Details.Tags.Add(Assembly.GetExecutingAssembly().GetInformationalVersion());

                client.Send(message);
            }
            catch (Exception ex)
            {
                log.Warn(ex);
            }
        }

        string GetAdminStatus()
        {
            try
            {
                var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                return isAdmin.ToString();
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                return "Unknown";
            }
        }

        static bool IsJoinedToDomain()
        {
            try
            {
                System.DirectoryServices.ActiveDirectory.Domain.GetComputerDomain();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool ReadFipsConfigValue()
        {
            // Mono does not currently support this method. Have this in a separate method to avoid JITing exceptions.
            var cryptoConfig = typeof(System.Security.Cryptography.CryptoConfig);

            if (cryptoConfig != null)
            {
                var allowOnlyFipsAlgorithmsProperty = cryptoConfig.GetProperty("AllowOnlyFipsAlgorithms", BindingFlags.NonPublic | BindingFlags.Static);

                if (allowOnlyFipsAlgorithmsProperty != null)
                {
                    return (bool)allowOnlyFipsAlgorithmsProperty.GetValue(null, null);
                }
            }

            return false;
        }
    }
}