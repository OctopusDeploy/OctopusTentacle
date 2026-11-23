using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    // Builds a Tentacle instance that is configured but not running, for tests that drive Tentacle through its commands.
    public class TentacleInstanceBuilder
    {
        Version? tentacleVersion = TentacleVersions.Current;
        string? tentacleExe;
        string? name = "Tentacle";
        TemporaryDirectory? home;
        bool useDefaultMachineConfigurationHomeDirectory;
        readonly Dictionary<string, string> environmentVariables = new();
        bool withNewCertificate;
        (string PfxPath, string Thumbprint)? certificate;

        internal Version? TentacleVersion => tentacleVersion;

        public TentacleInstanceBuilder WithTentacleVersion(Version? tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;
            return this;
        }

        public TentacleInstanceBuilder WithTentacleExe(string tentacleExe)
        {
            this.tentacleExe = tentacleExe;
            return this;
        }

        public TentacleInstanceBuilder WithName(string name)
        {
            this.name = name;
            return this;
        }

        // An instance with no name has no registry entry, so commands can only reach it through its configuration file.
        public TentacleInstanceBuilder UnnamedInstanceAddressedOnlyByConfig()
        {
            name = null;
            return this;
        }

        public TentacleInstanceBuilder WithHomeDirectory(TemporaryDirectory home)
        {
            this.home = home;
            return this;
        }

        // Registers the instance in the machine's real registry rather than one in its home directory.
        public TentacleInstanceBuilder UseDefaultMachineConfigurationHomeDirectory()
        {
            useDefaultMachineConfigurationHomeDirectory = true;
            return this;
        }

        public TentacleInstanceBuilder WithEnvironmentVariable(string name, string? value)
        {
            environmentVariables[name] = value!;
            return this;
        }

        public TentacleInstanceBuilder WithCertificate(string pfxPath, string thumbprint)
        {
            certificate = (pfxPath, thumbprint);
            return this;
        }

        public TentacleInstanceBuilder WithNewCertificate()
        {
            withNewCertificate = true;
            return this;
        }

        public ListeningRunningTentacleBuilder RunAsListeningTentacle(string serverThumbprint)
            => new(serverThumbprint, this);

        public PollingRunningTentacleBuilder RunAsPollingTentacle(int pollingPort, string serverThumbprint)
            => new(pollingPort, serverThumbprint, this);

        public async Task<TentacleInstance> Build(ILogger logger, CancellationToken cancellationToken)
        {
            var exe = tentacleExe ?? (tentacleVersion is null
                ? TentacleExeFinder.FindTentacleExe()
                : await TentacleFetcher.GetTentacleVersion(Path.GetTempPath(), tentacleVersion, DefaultTentacleRuntime.Value, logger, cancellationToken));
            var instanceHome = home ?? new TemporaryDirectory();
            var environment = new Dictionary<string, string>();
            if (!useDefaultMachineConfigurationHomeDirectory)
            {
                environment[EnvironmentVariables.TentacleMachineConfigurationHomeDirectory] = Path.Combine(instanceHome.DirectoryPath, "Octopus", "Tentacle", "Instances");
            }

            foreach (var environmentVariable in environmentVariables)
            {
                environment[environmentVariable.Key] = environmentVariable.Value;
            }

            var instance = new TentacleInstance(instanceHome, exe, name, thumbprint: null, environment, cancellationToken);
            if (instance.Name is null)
            {
                await File.WriteAllTextAsync(instance.ConfigurationFile, "<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>", cancellationToken);
            }
            else
            {
                await instance.ExecuteTentacleCommand(CommandResultValidation.ZeroExitCode, "create-instance", "--instance", instance.Name, "--config", instance.ConfigurationFile);
            }

            if (certificate != null)
            {
                await instance.ExecuteTentacleCommand(CommandResultValidation.ZeroExitCode, "import-certificate", $"--from-file={certificate.Value.PfxPath}", "--instance", instance.Name!);
                return instance.WithThumbprint(certificate.Value.Thumbprint);
            }

            if (withNewCertificate)
            {
                await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "new-certificate");
                return instance.WithThumbprint(await instance.ShowThumbprint());
            }

            return instance;
        }
    }

    // Everything the instance owns - its registry entry, configuration and data - lives in its home directory.
    public class TentacleInstance : IDisposable
    {
        readonly TemporaryDirectory home;
        readonly string tentacleExe;
        readonly IReadOnlyDictionary<string, string> environmentVariables;
        readonly CancellationToken cancellationToken;

        internal TentacleInstance(TemporaryDirectory home, string tentacleExe, string? name, string? thumbprint, IReadOnlyDictionary<string, string> environmentVariables, CancellationToken cancellationToken)
        {
            this.home = home;
            this.tentacleExe = tentacleExe;
            Name = name;
            Thumbprint = thumbprint;
            this.environmentVariables = environmentVariables;
            this.cancellationToken = cancellationToken;
        }

        public string? Name { get; }
        public string? Thumbprint { get; }
        internal TemporaryDirectory Home => home;
        internal string TentacleExe => tentacleExe;
        internal IReadOnlyDictionary<string, string> EnvironmentVariables => environmentVariables;
        public string ConfigurationFile => Path.Combine(home.DirectoryPath, (Name ?? "Tentacle") + ".cfg");

        // The same instance, as if the current Tentacle had been installed over it.
        public TentacleInstance WithCurrentTentacleExe()
            => new(home, TentacleExeFinder.FindTentacleExe(), Name, Thumbprint, environmentVariables, cancellationToken);

        internal TentacleInstance WithThumbprint(string thumbprint)
            => new(home, tentacleExe, Name, thumbprint, environmentVariables, cancellationToken);

        public async Task<string> ShowThumbprint()
            => (await ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "show-thumbprint")).StandardOutput.Trim();

        public async Task<BufferedCommandResult> ExecuteTentacleCommandOnTheInstance(CommandResultValidation validation, string command)
            => Name is null
                ? await ExecuteTentacleCommand(validation, command, "--config", ConfigurationFile)
                : await ExecuteTentacleCommand(validation, command, "--instance", Name);

        public async Task<BufferedCommandResult> ExecuteTentacleCommand(CommandResultValidation validation, params string[] arguments)
            => await Cli.Wrap(tentacleExe)
                .WithArguments(arguments)
                .WithValidation(validation)
                .WithWorkingDirectory(home.DirectoryPath)
                .WithEnvironmentVariables(environmentVariables)
                .ExecuteBufferedAsync(cancellationToken);

        public void Dispose() => home.Dispose();
    }
}
