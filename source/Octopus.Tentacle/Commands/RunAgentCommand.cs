using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Time;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Shared.Variables;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Versioning;

namespace Octopus.Tentacle.Commands
{
    public class RunAgentCommand : AbstractStandardCommand
    {
        readonly Lazy<IHalibutInitializer> halibut;
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly Lazy<IHomeConfiguration> home;
        readonly Lazy<IProxyConfiguration> proxyConfiguration;
        readonly ISleep sleep;
        readonly ILog log;
        readonly IApplicationInstanceSelector selector;
        readonly Lazy<IProxyInitializer> proxyInitializer;
        readonly AppVersion appVersion;
        int wait;

        public override bool CanUseNonInteractiveHost => true;

        public RunAgentCommand(
            Lazy<IHalibutInitializer> halibut,
            Lazy<ITentacleConfiguration> configuration,
            Lazy<IHomeConfiguration> home,
            Lazy<IProxyConfiguration> proxyConfiguration,
            ISleep sleep,
            ILog log,
            IApplicationInstanceSelector selector,
            Lazy<IProxyInitializer> proxyInitializer,
            AppVersion appVersion) : base(selector)
        {
            this.halibut = halibut;
            this.configuration = configuration;
            this.home = home;
            this.proxyConfiguration = proxyConfiguration;
            this.sleep = sleep;
            this.log = log;
            this.selector = selector;
            this.proxyInitializer = proxyInitializer;
            this.appVersion = appVersion;

            Options.Add("wait=", "Delay (ms) before starting", arg => wait = int.Parse(arg));
            Options.Add("console", "Don't attempt to run as a service, even if the user is non-interactive", v =>
            {
                // There's actually nothing to do here. The CommandHost should have already been determined before Start() was called
                // This option is added to show help
            });
        }

        protected override void Start()
        {
            if (wait >= 20)
            {
                log.Info("Sleeping for " + wait + "ms...");
                sleep.For(wait);
            }

            if (home.Value.HomeDirectory == null)
                throw new InvalidOperationException("No home directory has been configured for this Tentacle. Please run the configure command before starting.");

            if (configuration.Value.TentacleCertificate == null)
                throw new InvalidOperationException("No certificate has been generated for this Tentacle. Please run the new-certificate command before starting.");

            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleHome, home.Value.HomeDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleApplications, configuration.Value.ApplicationDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleJournal, configuration.Value.JournalFilePath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleInstanceName, selector.GetCurrentInstance().InstanceName);
            var exePath = typeof (RunAgentCommand).Assembly.FullLocalPath();
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleExecutablePath, exePath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProgramDirectoryPath, Path.GetDirectoryName(exePath));
            Environment.SetEnvironmentVariable(EnvironmentVariables.AgentProgramDirectoryPath, Path.GetDirectoryName(exePath));
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleVersion, appVersion.ToString());
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, configuration.Value.TentacleCertificate.SignatureAlgorithm.FriendlyName);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, proxyConfiguration.Value.CustomProxyUsername);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, proxyConfiguration.Value.CustomProxyPassword);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, proxyConfiguration.Value.CustomProxyHost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, proxyConfiguration.Value.CustomProxyPort.ToString());

            proxyInitializer.Value.InitializeProxy();

            halibut.Value.Start();

            Runtime.WaitForUserToExit();
        }

        protected override void Stop()
        {
            halibut.Value.Stop();
        }
    }
}