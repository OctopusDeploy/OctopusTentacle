using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using CliWrap;
using CliWrap.Exceptions;
using Nito.AsyncEx.Interop;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public interface ITentacleBuilder
    {
        ITentacleBuilder WithRunTentacleEnvironmentVariable<TValue>(string environmentVariable, TValue value);
        ITentacleBuilder WithHomeDirectory(TemporaryDirectory homeDirectory);
    }

    public abstract class TentacleBuilder<T> : ITentacleBuilder
        where T : TentacleBuilder<T>
    {
        protected string? ServerThumbprint;
        protected string? TentacleExePath;
        protected string CertificatePfxPath = Certificates.TentaclePfxPath;
        protected string TentacleThumbprint = Certificates.TentaclePublicThumbprint;
        
        readonly Regex listeningPortRegex = new (@"^listen:\/\/.+:(\d+)\/");
        readonly Dictionary<string, string> runTentacleEnvironmentVariables = new();

        TemporaryDirectory? homeDirectory;

        protected TemporaryDirectory HomeDirectory
        {
            get
            {
                homeDirectory ??= new TemporaryDirectory();
                return homeDirectory;
            }
        }

        public ITentacleBuilder WithHomeDirectory(TemporaryDirectory homeDirectory)
        {
            this.homeDirectory = homeDirectory;
            return this;
        }

        public T WithCertificate(string certificatePfxPath, string tentacleThumbprint)
        {
            CertificatePfxPath = certificatePfxPath;
            TentacleThumbprint = tentacleThumbprint;

            return (T)this;
        }

        public T WithTentacleExe(string tentacleExe)
        {
            TentacleExePath = tentacleExe;

            return (T)this;
        }

        public ITentacleBuilder WithRunTentacleEnvironmentVariable<TValue>(string environmentVariable, TValue value)
        {
            runTentacleEnvironmentVariables[environmentVariable] = value?.ToString();

            return this;
        }

        protected void WithWritableTentacleConfiguration(string configFilePath, Action<IWritableTentacleConfiguration> action)
        {
            var startUpConfigFileInstanceRequest = new StartUpConfigFileInstanceRequest(configFilePath);
            using var container = new Program(Array.Empty<string>()).BuildContainer(startUpConfigFileInstanceRequest);

            var writableTentacleConfiguration = container.Resolve<IWritableTentacleConfiguration>();
            action(writableTentacleConfiguration);
        }

        protected async Task<RunningTentacle> StartTentacle(
            Uri? serviceUri,
            string tentacleExe,
            string instanceName,
            TemporaryDirectory tempDirectory,
            string applicationDirectory,
            string tentacleThumbprint,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var runningTentacle = new RunningTentacle(
                tempDirectory,
                startTentacleFunction: ct => RunTentacle(serviceUri, tentacleExe, instanceName, tempDirectory, ct),
                tentacleThumbprint,
                instanceName,
                HomeDirectory.DirectoryPath,
                applicationDirectory,
                deleteInstanceFunction: ct => DeleteInstanceIgnoringFailure(tentacleExe, instanceName, tempDirectory, logger, ct),
                logger);

            try
            {
                await runningTentacle.Start(cancellationToken);
                return runningTentacle;
            }
            catch (Exception)
            {
                try
                {
                    await runningTentacle.DisposeAsync();
                }
                catch (Exception e)
                {
                    new SerilogLoggerBuilder().Build().Information(e, "Error disposing tentacle after tentacle failed to start.");
                }

                throw;
            }
        }

        async Task<(Task task, Uri serviceUri)> RunTentacle(
            Uri? serviceUri,
            string tentacleExe,
            string instanceName,
            TemporaryDirectory tempDirectory, 
            CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();
            int? listeningPort = null;

            var runningTentacle = Task.Run(async () =>
            {
                try
                {
                    await RunTentacleCommandOutOfProcess(
                        tentacleExe, 
                        new[] {"agent", $"--instance={instanceName}", "--noninteractive"}, 
                        tempDirectory,
                        s =>
                        {
                            if (s.Contains("Listener started"))
                            {
                                listeningPort = Convert.ToInt32(listeningPortRegex.Match(s).Groups[1].Value);
                            }
                            else if (s.Contains("Agent will not listen") || s.Contains("Agent listening on"))
                            {
                                hasTentacleStarted.Set();
                            }
                        },
                        runTentacleEnvironmentVariables, 
                        cancellationToken);
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(e);
                    throw;
                }
            }, cancellationToken);

            await Task.WhenAny(runningTentacle, WaitHandleAsyncFactory.FromWaitHandle(hasTentacleStarted.WaitHandle, TimeSpan.FromMinutes(1)));

            // Will throw.
            if (runningTentacle.IsCompleted)
            {
                await runningTentacle;
            }

            if (!hasTentacleStarted.IsSet)
            {
                throw new Exception("Tentacle did not appear to start correctly");
            }

            if (serviceUri == null && listeningPort != null)
            {
                serviceUri = new Uri($"https://localhost:{listeningPort}");
            }

            return (runningTentacle, serviceUri);
        }

        protected async Task AddCertificateToTentacle(string tentacleExe, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            await RunTentacleCommand(tentacleExe, new[] {"import-certificate", $"--from-file={tentaclePfxPath}", $"--instance={instanceName}"}, tmp, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected async Task CreateInstance(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            await RunTentacleCommand(tentacleExe, new[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal async Task DeleteInstanceIgnoringFailure(string tentacleExe, string instanceName, TemporaryDirectory tmp, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                await DeleteInstanceAsync(tentacleExe, instanceName, tmp, cancellationToken);
            }
            catch (Exception e)
            {
                logger.Warning(e, "Could not delete instance: {InstanceName}", instanceName);
                throw;
            }
        }

        internal async Task DeleteInstanceAsync(string tentacleExe, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            await RunTentacleCommand(tentacleExe, new[] {"delete-instance", $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        internal string InstanceNameGenerator()
        {
            return $"TentacleIT-{Guid.NewGuid():N}";
        }

        async Task RunTentacleCommand(string tentacleExe, string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            await RunTentacleCommandOutOfProcess(
                tentacleExe,
                args, 
                tmp, 
                _ => { }, 
                new Dictionary<string, string?>(), 
                cancellationToken);
        }

        async Task RunTentacleCommandOutOfProcess(
            string tentacleExe,
            string[] args,
            TemporaryDirectory tmp,
            Action<string> commandOutput, 
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            var log = new SerilogLoggerBuilder().Build().ForContext<RunningTentacle>();

            async Task ProcessLogs(string s, CancellationToken ct)
            {
                await Task.CompletedTask;
                log.Information("[Tentacle] " + s);
                commandOutput(s);
            }

            try
            {
                var commandResult = await RetryHelper.RetryAsync<CommandResult, CommandExecutionException>(
                    () => Cli.Wrap(tentacleExe)
                        .WithArguments(args)
                        .WithEnvironmentVariables(environmentVariables)
                        .WithWorkingDirectory(tmp.DirectoryPath)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(ProcessLogs))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(ProcessLogs))
                        .ExecuteAsync(cancellationToken));

                if (cancellationToken.IsCancellationRequested) return;

                if (commandResult.ExitCode != 0)
                {
                    throw new Exception("Tentacle returns non zero exit code: " + commandResult.ExitCode);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
