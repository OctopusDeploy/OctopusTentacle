using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    // Builds a Tentacle instance that is configured but not running, for tests that drive Tentacle through its commands.
    public class TentacleInstanceBuilder
    {
        Version? tentacleVersion = TentacleVersions.Current;
        string? tentacleExe;
        string? name = "Tentacle";
        bool nameChosen;
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
            nameChosen = true;
            return this;
        }

        // An instance with no name has no registry entry, so commands can only reach it through its configuration file.
        public TentacleInstanceBuilder UnnamedInstanceAddressedOnlyByConfig()
        {
            name = null;
            nameChosen = true;
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

        // Fills in what a running Tentacle needs, keeping anything the caller chose.
        internal TentacleInstanceBuilder WithDefaultsForRunning(string defaultName, string pfxPath, string thumbprint)
        {
            if (!nameChosen)
            {
                name = defaultName;
            }
            else if (name == null)
            {
                throw new InvalidOperationException("An unnamed instance cannot be run, the agent is started with --instance");
            }

            if (certificate == null && !withNewCertificate)
            {
                certificate = (pfxPath, thumbprint);
            }

            return this;
        }

        public ListeningRunningTentacleBuilder RunAsListeningTentacle(string serverThumbprint)
            => new(serverThumbprint, this);

        public PollingRunningTentacleBuilder RunAsPollingTentacle(int pollingPort, string serverThumbprint)
            => new(pollingPort, serverThumbprint, this);

        public async Task<TentacleInstance> Build(ILogger logger, CancellationToken cancellationToken)
        {
            using var download = new TemporaryDirectory();
            var exe = tentacleExe ?? (tentacleVersion is null
                ? TentacleExeFinder.FindTentacleExe()
                : await TentacleFetcher.GetTentacleVersion(download.DirectoryPath, tentacleVersion, DefaultTentacleRuntime.Value, logger, cancellationToken));
            var instanceHome = home ?? new TemporaryDirectory();
            var environment = new Dictionary<string, string>
            {
                [EnvironmentVariables.TentacleInstancesDataDirectory] = Path.Combine(instanceHome.DirectoryPath, "data")
            };
            if (!useDefaultMachineConfigurationHomeDirectory)
            {
                environment[EnvironmentVariables.TentacleMachineConfigurationHomeDirectory] = Path.Combine(instanceHome.DirectoryPath, "Octopus", "Tentacle", "Instances");
            }

            foreach (var environmentVariable in environmentVariables)
            {
                environment[environmentVariable.Key] = environmentVariable.Value;
            }

            var instance = new TentacleInstance(instanceHome, exe, name, thumbprint: null, environment);
            if (instance.Name is null)
            {
                await File.WriteAllTextAsync(instance.ConfigurationFile, "<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>", cancellationToken);
            }
            else
            {
                await instance.ExecuteTentacleCommand(CommandResultValidation.ZeroExitCode, cancellationToken, "create-instance", "--instance", instance.Name, "--config", instance.ConfigurationFile);
            }

            if (certificate != null)
            {
                await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "import-certificate", cancellationToken, $"--from-file={certificate.Value.PfxPath}");
                return instance.WithThumbprint(certificate.Value.Thumbprint);
            }

            if (withNewCertificate)
            {
                await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "new-certificate", cancellationToken);
                return instance.WithThumbprint(await instance.ShowThumbprint(cancellationToken));
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

        internal TentacleInstance(TemporaryDirectory home, string tentacleExe, string? name, string? thumbprint, IReadOnlyDictionary<string, string> environmentVariables)
        {
            this.home = home;
            this.tentacleExe = tentacleExe;
            Name = name;
            Thumbprint = thumbprint;
            this.environmentVariables = environmentVariables;
        }

        public string? Name { get; }
        public string? Thumbprint { get; }
        internal TemporaryDirectory Home => home;
        internal string TentacleExe => tentacleExe;
        internal IReadOnlyDictionary<string, string> EnvironmentVariables => environmentVariables;
        public string ConfigurationFile => Path.Combine(home.DirectoryPath, (Name ?? "Tentacle") + ".cfg");
        public string DataDirectory => Path.Combine(home.DirectoryPath, "data");
        public string ConfigurationEncryptionKeyFile => Path.Combine(DataDirectory, Name!, "keys", ConfigurationEncryptionKey.KeyFileName);

        // The same instance, as if the current Tentacle had been installed over it.
        public TentacleInstance WithCurrentTentacleExe()
            => new(home, TentacleExeFinder.FindTentacleExe(), Name, Thumbprint, environmentVariables);

        internal TentacleInstance WithThumbprint(string thumbprint)
            => new(home, tentacleExe, Name, thumbprint, environmentVariables);

        public string CertificateAsStoredInTheConfigurationFile()
            => XDocument.Load(ConfigurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == TentacleConfiguration.CertificateSettingName).Value;

        public async Task<string> ShowThumbprint(CancellationToken cancellationToken)
            => (await ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "show-thumbprint", cancellationToken)).StandardOutput.Trim();

        public async Task<BufferedCommandResult> ExecuteTentacleCommandOnTheInstance(CommandResultValidation validation, string command, CancellationToken cancellationToken, params string[] arguments)
            => await ExecuteTentacleCommand(validation, cancellationToken, new[] { command }
                .Concat(arguments)
                .Concat(Name is null ? new[] { "--config", ConfigurationFile } : new[] { "--instance", Name })
                .ToArray());

        string KeyDirectory => Path.GetDirectoryName(ConfigurationEncryptionKeyFile)!;

        internal async Task SudoAllowEveryoneToReadTheHomeDirectory()
            => await Sudo("chmod", "-R", "o+rX", home.DirectoryPath);

        internal async Task SudoMakeTheKeyDirectoryOwnedAndOnlyAccessibleByRoot()
        {
            await Sudo("chown", "-R", "root:root", KeyDirectory);
            await Sudo("chmod", "700", KeyDirectory);
            await Sudo("chmod", "600", ConfigurationEncryptionKeyFile);
        }

        // The test runner cannot delete a directory owned by root, so it must be deleted before the home directory is.
        internal async Task SudoDeleteTheKeyDirectory()
            => await Sudo("rm", "-rf", KeyDirectory);

        internal async Task SudoGiveTheConfigurationFileTo(LinuxTestUserPrincipal user)
            => await Sudo("chown", user.UserName, ConfigurationFile);

        // Root owns everything a root Tentacle creates in the data directory, so it must be deleted before the home directory is.
        internal async Task SudoDeleteTheDataDirectory()
            => await Sudo("rm", "-rf", DataDirectory);

        static async Task Sudo(params string[] arguments)
            => await Cli.Wrap("sudo").WithArguments(arguments).ExecuteBufferedAsync();

        // sudo resets the environment, so the instance's environment is passed through env instead.
        internal async Task<BufferedCommandResult> ExecuteTentacleCommandOnTheInstanceAsUser(LinuxTestUserPrincipal user, CommandResultValidation validation, string command, CancellationToken cancellationToken, params string[] arguments)
        {
            var sudoArguments = new[] { "-u", user.UserName, "env" }
                .Concat(environmentVariables.Select(e => $"{e.Key}={e.Value}"))
                .Concat(new[] { tentacleExe, command })
                .Concat(arguments)
                .Concat(Name is null ? new[] { "--config", ConfigurationFile } : new[] { "--instance", Name });
            return await Cli.Wrap("sudo")
                .WithArguments(sudoArguments)
                .WithValidation(validation)
                .WithWorkingDirectory(home.DirectoryPath)
                .ExecuteBufferedAsync(cancellationToken);
        }

        internal async Task<BufferedCommandResult> ExecuteTentacleCommandOnTheInstanceAsRootWithUmask(string umask, CommandResultValidation validation, string command, CancellationToken cancellationToken)
        {
            var sudoArguments = new[] { "sh", "-c", $"umask {umask} && exec \"$@\"", "sh", "env" }
                .Concat(environmentVariables.Select(e => $"{e.Key}={e.Value}"))
                .Concat(new[] { tentacleExe, command })
                .Concat(Name is null ? new[] { "--config", ConfigurationFile } : new[] { "--instance", Name });
            return await Cli.Wrap("sudo")
                .WithArguments(sudoArguments)
                .WithValidation(validation)
                .WithWorkingDirectory(home.DirectoryPath)
                .ExecuteBufferedAsync(cancellationToken);
        }

        // Older Tentacles share the machine's instance registry, so a command can fail while another test registers an instance.
        public async Task<BufferedCommandResult> ExecuteTentacleCommand(CommandResultValidation validation, CancellationToken cancellationToken, params string[] arguments)
            => await RetryHelper.RetryAsync<BufferedCommandResult, CommandExecutionException>(
                async () => await Cli.Wrap(tentacleExe)
                    .WithArguments(arguments)
                    .WithValidation(validation)
                    .WithWorkingDirectory(home.DirectoryPath)
                    .WithEnvironmentVariables(environmentVariables)
                    .ExecuteBufferedAsync(cancellationToken));

        public void Dispose() => home.Dispose();
    }
}
