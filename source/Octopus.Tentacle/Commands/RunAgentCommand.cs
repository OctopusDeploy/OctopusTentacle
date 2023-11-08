using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using Octopus.Diagnostics;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Maintenance;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;
using Octopus.Tentacle.Versioning;
using Octopus.Time;

namespace Octopus.Tentacle.Commands
{
    public class RunAgentCommand : AbstractStandardCommand
    {
        // The lazy dependencies allow the class to be created when its dependencies are not initialised yet.
        // This is needed when calling commands like 'help' or providing feedback on validation (e.g. when no --instance parameter provided)
        readonly Lazy<IHalibutInitializer> halibut;
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly Lazy<IHomeConfiguration> home;
        readonly Lazy<IProxyConfiguration> proxyConfiguration;
        readonly Lazy<IProxyInitializer> proxyInitializer;
        readonly Lazy<IWorkspaceCleanerTask> workspaceCleanerTask;

        readonly ISleep sleep;
        readonly ISystemLog log;
        readonly IApplicationInstanceSelector selector;
        readonly IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;
        readonly AppVersion appVersion;
        int wait;
        bool halibutHasStarted;
        bool workspaceCleanerHasStarted;

        public override bool CanRunAsService => true;

        public RunAgentCommand(
            Lazy<IHalibutInitializer> halibut,
            Lazy<IWritableTentacleConfiguration> configuration,
            Lazy<IHomeConfiguration> home,
            Lazy<IProxyConfiguration> proxyConfiguration,
            ISleep sleep,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            Lazy<IProxyInitializer> proxyInitializer,
            IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker,
            AppVersion appVersion,
            ILogFileOnlyLogger logFileOnlyLogger,
            Lazy<IWorkspaceCleanerTask> workspaceCleanerTask) : base(selector, log, logFileOnlyLogger)
        {
            this.halibut = halibut;
            this.configuration = configuration;
            this.home = home;
            this.proxyConfiguration = proxyConfiguration;
            this.sleep = sleep;
            this.log = log;
            this.selector = selector;
            this.proxyInitializer = proxyInitializer;
            this.windowsLocalAdminRightsChecker = windowsLocalAdminRightsChecker;
            this.appVersion = appVersion;
            this.workspaceCleanerTask = workspaceCleanerTask;

            Options.Add("wait=", "Delay (ms) before starting", arg => wait = int.Parse(arg));
            Options.Add("console", "Don't attempt to run as a service, even if the user is non-interactive", v =>
            {
                // There's actually nothing to do here. The CommandHost should have already been determined before Start() was called
                // This option is added to show help
            });
        }

        protected override void Start()
        {
            base.Start();

            if (wait >= 20)
            {
                log.Info("Sleeping for " + wait + "ms...");
                sleep.For(wait);
            }

            try
            {
                if (configuration.Value.TentacleCertificate == null)
                {
                    var certificate = configuration.Value.GenerateNewCertificate();
                    log.Info("A new certificate has been generated and installed as none were yet available. Thumbprint:");
                    log.Info(certificate.Thumbprint);
                }
            }
            catch (CryptographicException cx)
            {
                log.Error($"The owner of the x509stores is not the current user, please change ownership of the x509stores directory or run with sudo. Details: {cx.Message}");
                return;
            }

            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleHome, home.Value.HomeDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleApplications, configuration.Value.ApplicationDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleJournal, configuration.Value.JournalFilePath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.CalamariPackageRetentionJournalPath, configuration.Value.PackageRetentionJournalPath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleInstanceName, selector.Current.InstanceName);
            var currentPath = typeof(RunAgentCommand).Assembly.FullLocalPath();
            var exePath = PlatformDetection.IsRunningOnWindows
                ? Path.ChangeExtension(currentPath, "exe")
                : Path.Combine(Path.GetDirectoryName(currentPath) ?? string.Empty, Path.GetFileNameWithoutExtension(currentPath) ?? string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleExecutablePath, exePath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProgramDirectoryPath, Path.GetDirectoryName(exePath));
            Environment.SetEnvironmentVariable(EnvironmentVariables.AgentProgramDirectoryPath, Path.GetDirectoryName(exePath));
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleVersion, appVersion.ToString());
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleNetFrameworkDescription, RuntimeInformation.FrameworkDescription);
            if (configuration.Value.TentacleCertificate != null)
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, configuration.Value.TentacleCertificate.SignatureAlgorithm.FriendlyName);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy, proxyConfiguration.Value.UseDefaultProxy.ToString());
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, proxyConfiguration.Value.CustomProxyUsername);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, proxyConfiguration.Value.CustomProxyPassword);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, proxyConfiguration.Value.CustomProxyHost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, proxyConfiguration.Value.CustomProxyPort.ToString());

            LogWarningIfNotRunningAsAdministrator();

            proxyInitializer.Value.InitializeProxy();

            halibut.Value.Start();
            halibutHasStarted = true;

            workspaceCleanerTask.Value.Start();
            workspaceCleanerHasStarted = true;

            Runtime.WaitForUserToExit();
        }

        void LogWarningIfNotRunningAsAdministrator()
        {
            if (!PlatformDetection.IsRunningOnWindows) return;

            if (windowsLocalAdminRightsChecker.IsRunningElevated()) return;

#pragma warning disable CA1416
            var groupName = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)).Value;
            log.Warn($"Tentacle is not running with elevated permissions (user '{WindowsIdentity.GetCurrent().Name}' is not a member of '{groupName}'). Some functionality may be impaired.");
#pragma warning restore CA1416
        }

        protected override void Stop()
        {
            if (halibutHasStarted)
            {
                halibut.Value.Stop();
            }

            if (workspaceCleanerHasStarted)
            {
                workspaceCleanerTask.Value.Stop();
            }
        }
    }
}
