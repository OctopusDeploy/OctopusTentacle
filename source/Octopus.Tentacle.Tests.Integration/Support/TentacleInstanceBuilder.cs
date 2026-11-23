using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    // Builds a Tentacle instance that is configured but not running, for tests that drive Tentacle through its commands.
    public class TentacleInstanceBuilder
    {
        Version? tentacleVersion;
        bool withNewCertificate;

        public TentacleInstanceBuilder WithTentacleVersion(Version tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;
            return this;
        }

        public TentacleInstanceBuilder WithNewCertificate()
        {
            withNewCertificate = true;
            return this;
        }

        public async Task<TentacleInstance> Build(ILogger logger, CancellationToken cancellationToken)
        {
            var tentacleExe = tentacleVersion == null
                ? TentacleExeFinder.FindTentacleExe()
                : await TentacleFetcher.GetTentacleVersion(Path.GetTempPath(), tentacleVersion, DefaultTentacleRuntime.Value, logger, cancellationToken);

            var instance = new TentacleInstance(tentacleExe, cancellationToken);
            await instance.Run("create-instance", "--instance", instance.Name, "--config", instance.ConfigurationFile);
            if (withNewCertificate)
            {
                await instance.Run("new-certificate", "--instance", instance.Name);
            }

            return instance;
        }
    }

    // Everything the instance owns - its registry entry, configuration and data - lives in a temporary home directory.
    public class TentacleInstance : IDisposable
    {
        readonly TemporaryDirectory home = new();
        readonly CancellationToken cancellationToken;

        internal TentacleInstance(string tentacleExe, CancellationToken cancellationToken)
        {
            TentacleExe = tentacleExe;
            this.cancellationToken = cancellationToken;
        }

        public string Name => "Tentacle";
        public string TentacleExe { get; }
        public string ConfigurationFile => Path.Combine(home.DirectoryPath, "tentacle.config");
        public string DataDirectory => Path.Combine(home.DirectoryPath, "data");

        public Task<BufferedCommandResult> Run(params string[] arguments)
            => RunWith(TentacleExe, CommandResultValidation.ZeroExitCode, arguments);

        public Task<BufferedCommandResult> RunWith(string tentacleExe, params string[] arguments)
            => RunWith(tentacleExe, CommandResultValidation.ZeroExitCode, arguments);

        public Task<BufferedCommandResult> TryRunWith(string tentacleExe, params string[] arguments)
            => RunWith(tentacleExe, CommandResultValidation.None, arguments);

        Task<BufferedCommandResult> RunWith(string tentacleExe, CommandResultValidation validation, string[] arguments)
            => Cli.Wrap(tentacleExe)
                .WithArguments(arguments)
                .WithValidation(validation)
                .WithEnvironmentVariables(e => e
                    .Set(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, Path.Combine(home.DirectoryPath, "home"))
                    .Set(EnvironmentVariables.TentacleInstancesDataDirectory, DataDirectory))
                .ExecuteBufferedAsync(cancellationToken);

        public void Dispose() => home.Dispose();
    }
}
