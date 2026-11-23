using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    // Builds a Tentacle instance that is configured but not running, for tests that drive Tentacle through its commands.
    public class TentacleInstanceBuilder
    {
        Version? tentacleVersion = TentacleVersions.Current;
        bool withNewCertificate;
        bool unnamed;

        public TentacleInstanceBuilder WithTentacleVersion(Version? tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;
            return this;
        }

        // An instance with no name has no registry entry, so commands can only reach it through its configuration file.
        public TentacleInstanceBuilder UnnamedInstanceAddressedOnlyByConfig()
        {
            unnamed = true;
            return this;
        }

        public TentacleInstanceBuilder WithNewCertificate()
        {
            withNewCertificate = true;
            return this;
        }

        public async Task<TentacleInstance> Build(ILogger logger, CancellationToken cancellationToken)
        {
            var tentacleExe = tentacleVersion is null
                ? TentacleExeFinder.FindTentacleExe()
                : await TentacleFetcher.GetTentacleVersion(Path.GetTempPath(), tentacleVersion, DefaultTentacleRuntime.Value, logger, cancellationToken);

            var instance = new TentacleInstance(tentacleExe, unnamed ? null : "Tentacle", cancellationToken);
            if (instance.Name is null)
            {
                await File.WriteAllTextAsync(instance.ConfigurationFile, "<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>", cancellationToken);
            }
            else
            {
                await instance.ExecuteTentacleCommand(CommandResultValidation.ZeroExitCode, "create-instance", "--instance", instance.Name, "--config", instance.ConfigurationFile);
            }

            if (withNewCertificate)
            {
                await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "new-certificate");
            }

            return instance;
        }
    }

    // Everything the instance owns - its registry entry, configuration and data - lives in a temporary home directory.
    public class TentacleInstance : IDisposable
    {
        readonly TemporaryDirectory home;
        readonly string tentacleExe;
        readonly CancellationToken cancellationToken;

        internal TentacleInstance(string tentacleExe, string? name, CancellationToken cancellationToken) : this(new TemporaryDirectory(), tentacleExe, name, cancellationToken)
        {
        }

        TentacleInstance(TemporaryDirectory home, string tentacleExe, string? name, CancellationToken cancellationToken)
        {
            this.home = home;
            this.tentacleExe = tentacleExe;
            Name = name;
            this.cancellationToken = cancellationToken;
        }

        public string? Name { get; }
        public string ConfigurationFile => Path.Combine(home.DirectoryPath, "tentacle.config");
        public string DataDirectory => Path.Combine(home.DirectoryPath, "data");
        public string ConfigurationEncryptionKeyFile => Path.Combine(DataDirectory, Name!, "keys", ConfigurationEncryptionKey.KeyFileName);

        // The same instance, as if the current Tentacle had been installed over it.
        public TentacleInstance WithCurrentTentacleExe()
            => new(home, TentacleExeFinder.FindTentacleExe(), Name, cancellationToken);

        public string CertificateAsStoredInTheConfigurationFile()
            => XDocument.Load(ConfigurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == TentacleConfiguration.CertificateSettingName).Value;

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
                .WithEnvironmentVariables(e => e
                    .Set(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, Path.Combine(home.DirectoryPath, "home"))
                    .Set(EnvironmentVariables.TentacleInstancesDataDirectory, DataDirectory))
                .ExecuteBufferedAsync(cancellationToken);

        public void Dispose() => home.Dispose();
    }
}
