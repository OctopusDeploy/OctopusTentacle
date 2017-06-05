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
using AssemblyExtensions = Octopus.Tentacle.Versioning.AssemblyExtensions;

namespace Octopus.Tentacle.Commands
{
    public class RunAgentCommand : AbstractStandardCommand
    {
        readonly IHalibutInitializer halibut;
        readonly ITentacleConfiguration configuration;
        readonly IHomeConfiguration home;
        readonly IProxyConfiguration proxyConfiguration;
        readonly ISleep sleep;
        readonly ILog log;
        readonly IApplicationInstanceSelector selector;
        readonly IProxyInitializer proxyInitializer;
        readonly AppVersion appVersion;
        int wait;

        public RunAgentCommand(
            IHalibutInitializer halibut,
            ITentacleConfiguration configuration,
            IHomeConfiguration home,
            IProxyConfiguration proxyConfiguration,
            ISleep sleep,
            ILog log,
            IApplicationInstanceSelector selector,
            IProxyInitializer proxyInitializer,
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
        }

        protected override void Start()
        {
            if (wait >= 20)
            {
                log.Info("Sleeping for " + wait + "ms...");
                sleep.For(wait);
            }

            if (home.HomeDirectory == null)
                throw new InvalidOperationException("No home directory has been configured for this Tentacle. Please run the configure command before starting.");

            if (configuration.TentacleCertificate == null)
                throw new InvalidOperationException("No certificate has been generated for this Tentacle. Please run the new-certificate command before starting.");

            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleHome, home.HomeDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleApplications, configuration.ApplicationDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleJournal, configuration.JournalFilePath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleInstanceName, selector.Current.InstanceName);
            var exePath = AssemblyExtensions.FullLocalPath(typeof (RunAgentCommand).Assembly);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleExecutablePath, exePath);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProgramDirectoryPath, Path.GetDirectoryName(exePath));
            Environment.SetEnvironmentVariable(EnvironmentVariables.AgentProgramDirectoryPath, Path.GetDirectoryName(exePath));
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleVersion, appVersion.ToString());
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, configuration.TentacleCertificate.SignatureAlgorithm.FriendlyName);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, proxyConfiguration.CustomProxyUsername);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, proxyConfiguration.CustomProxyPassword);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, proxyConfiguration.CustomProxyHost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, proxyConfiguration.CustomProxyPort.ToString());

            proxyInitializer.InitializeProxy();

            halibut.Start();

            Runtime.WaitForUserToExit();
        }

        protected override void Stop()
        {
            halibut.Stop();
        }
    }
}