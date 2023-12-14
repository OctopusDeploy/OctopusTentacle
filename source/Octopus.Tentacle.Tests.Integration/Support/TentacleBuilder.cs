﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using CliWrap;
using CliWrap.Exceptions;
using Microsoft.Win32;
using Nito.AsyncEx.Interop;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
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
        bool installAsService = false;

        static readonly Regex ListeningPortRegex = new (@"listen:\/\/.+:(\d+)\/");
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

        public ITentacleBuilder InstallAsAService()
        {
            installAsService = true;

            return this;
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
            var log = new SerilogLoggerBuilder().Build().ForContext<ITentacleBuilder>();

            var runningTentacle = new RunningTentacle(
                new FileInfo(tentacleExe),
                tempDirectory,
                startTentacleFunction: ct => RunTentacle(serviceUri, tentacleExe, instanceName, tempDirectory, log, ct),
                tentacleThumbprint,
                instanceName,
                HomeDirectory.DirectoryPath,
                applicationDirectory,
                deleteInstanceFunction: ct => DeleteInstanceIgnoringFailure(installAsService, tentacleExe, instanceName, tempDirectory, logger, ct),
                runTentacleEnvironmentVariables,
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
                    log.Information(e, "Error disposing tentacle after tentacle failed to start.");
                }

                throw;
            }
        }

        async Task<(Task task, Uri serviceUri)> RunTentacle(
            Uri? serviceUri,
            string tentacleExe,
            string instanceName,
            TemporaryDirectory tempDirectory, 
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();
            int? listeningPort = null;

            var runningTentacle = Task.Run(async () =>
            {
                try
                {
                    if (installAsService)
                    {
                        var serviceInstalled = false;
                        var serviceStarted = false;

                        await RunCommandOutOfProcess(
                            tentacleExe, 
                            new[] {"service", "--install", $"--instance={instanceName}"}, 
                            "Tentacle",
                            tempDirectory,
                            s =>
                            {
                                if (s.Contains("Service installed"))
                                {
                                    serviceInstalled = true;
                                }
                            },
                            runTentacleEnvironmentVariables, 
                            logger,
                            cancellationToken);

                        // Set environment variables for the Service
                        if (PlatformDetection.IsRunningOnWindows)
                        {
                            var environment = runTentacleEnvironmentVariables.Select(x => $"{x.Key}={x.Value}").ToArray();

#pragma warning disable CA1416
                            Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\OctopusDeploy Tentacle: {instanceName}", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.SetValue)
                                .SetValue("Environment", environment, RegistryValueKind.MultiString);
#pragma warning restore CA1416
                        }
                        else
                        {
                            var environment = string.Join("", runTentacleEnvironmentVariables.Select(x => $"Environment={x.Key}={x.Value}{Environment.NewLine}"));

                            var systemdDirectoryInfo = new DirectoryInfo("/etc/systemd/system");
                            var serviceFileInfo = systemdDirectoryInfo.GetFiles($"{instanceName}.service").FirstOrDefault();

                            var service = await File.ReadAllTextAsync(serviceFileInfo.FullName, cancellationToken);
                            service = service.Replace("[Service]", $"[Service]{environment}{Environment.NewLine}");
                            await File.WriteAllTextAsync(serviceFileInfo.FullName, service, cancellationToken);
                            
                            await RunCommandOutOfProcess(
                                "systemctl", 
                                new[] {"daemon-reload"}, 
                                "systemctl",
                                tempDirectory,
                                s => {},
                                new Dictionary<string, string>(),
                                logger,
                                cancellationToken);
                        }

                        if (!serviceInstalled)
                        {
                            throw new Exception("Failed to install service");
                        }

                        await RunCommandOutOfProcess(
                            tentacleExe,
                            new[] { "service", "--start", $"--instance={instanceName}" },
                            "Tentacle",
                            tempDirectory,
                            s =>
                            {
                                if (s.Contains("Service started"))
                                {
                                    serviceStarted = true;
                                }
                            },
                            runTentacleEnvironmentVariables,
                            logger,
                            cancellationToken);

                        if (!serviceStarted)
                        {
                            throw new Exception("Failed to start service");
                        }

                        logger.Information("Waiting for the Tentacle Service to start up and assign a Listening Port / no port if Polling");
                        var tentacleState = await WaitForTentacleToStart(tempDirectory, cancellationToken);
                        
                        if (tentacleState.Started)
                        {
                            listeningPort = tentacleState.ListeningPort;
                            hasTentacleStarted.Set();
                        }
                        else
                        {
                            logger.Warning("The Tentacle failed to start correctly.");
                            logger.Warning(tentacleState.LogContent); }
                    }
                    else
                    {
                        await RunCommandOutOfProcess(
                            tentacleExe, 
                            new[] {"agent", $"--instance={instanceName}", "--noninteractive"}, 
                            "Tentacle",
                            tempDirectory,
                            s =>
                            {
                                if (s.Contains("Listener started"))
                                {
                                    listeningPort = Convert.ToInt32(ListeningPortRegex.Match(s).Groups[1].Value);
                                }
                                else if (s.Contains("Agent will not listen") || s.Contains("Agent listening on"))
                                {
                                    hasTentacleStarted.Set();
                                }
                            },
                            runTentacleEnvironmentVariables, 
                            logger,
                            cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(e);
                    throw;
                }
            }, cancellationToken);

            await Task.WhenAny(runningTentacle, WaitHandleAsyncFactory.FromWaitHandle(hasTentacleStarted.WaitHandle, TimeSpan.FromMinutes(1), cancellationToken));

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

        static async Task<(bool Started, int? ListeningPort, string LogContent)> WaitForTentacleToStart(TemporaryDirectory tempDirectory, CancellationToken localCancellationToken)
        {
            var lastLogFileContents = string.Empty;
            int? listeningPort = null;

            while (listeningPort == null && !localCancellationToken.IsCancellationRequested)
            {
                var logFilePath = Path.Combine(tempDirectory.DirectoryPath, "Logs", "OctopusTentacle.txt");

                await using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var logContent = await reader.ReadToEndAsync();
                    lastLogFileContents = logContent;
                }

                if (lastLogFileContents.Contains("Listener started"))
                {
                    listeningPort = Convert.ToInt32(ListeningPortRegex.Match(lastLogFileContents).Groups[1].Value);
                }

                if (lastLogFileContents.Contains("Agent will not listen") || lastLogFileContents.Contains("Agent listening on"))
                {
                    return (true, listeningPort, lastLogFileContents);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }

            return (false, listeningPort, lastLogFileContents);
        }

        protected async Task AddCertificateToTentacle(string tentacleExe, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, ILogger logger, CancellationToken cancellationToken)
        {
            await RunTentacleCommand(tentacleExe, new[] {"import-certificate", $"--from-file={tentaclePfxPath}", $"--instance={instanceName}"}, tmp, logger, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected async Task CreateInstance(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, ILogger logger, CancellationToken cancellationToken)
        {
            await RunTentacleCommand(tentacleExe, new[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, logger,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal async Task DeleteInstanceIgnoringFailure(bool runningAsService, string tentacleExe, string instanceName, TemporaryDirectory tmp, ILogger logger, CancellationToken cancellationToken)
        {
            if (runningAsService)
            {
                try
                {
                    await RunCommandOutOfProcess(
                        tentacleExe,
                        new[] { "service", $"--instance={instanceName}", "--stop" },
                        "Tentacle",
                        tmp,
                        s => {},
                        runTentacleEnvironmentVariables,
                        logger,
                        cancellationToken);

                    await RunCommandOutOfProcess(
                        tentacleExe, 
                        new[] {"service", "--uninstall", $"--instance={instanceName}"}, 
                        "Tentacle",
                        tmp,
                        s => {},
                        runTentacleEnvironmentVariables, 
                        logger,
                        cancellationToken);
                }
                catch (Exception e)
                {
                    logger.Warning(e, "Could not uninstall service for instance: {InstanceName}", instanceName);
                    throw;
                }
            }

            try
            {
                await DeleteInstanceAsync(tentacleExe, instanceName, tmp, logger,cancellationToken);
            }
            catch (Exception e)
            {
                logger.Warning(e, "Could not delete instance: {InstanceName}", instanceName);
                throw;
            }
        }

        internal async Task DeleteInstanceAsync(string tentacleExe, string instanceName, TemporaryDirectory tmp, ILogger logger, CancellationToken cancellationToken)
        {
            await RunTentacleCommand(tentacleExe, new[] {"delete-instance", $"--instance={instanceName}"}, tmp, logger, cancellationToken);
        }

        internal string InstanceNameGenerator()
        {
            // Ensure this is lower case to avoid issues with tests on linux with case sensitive file paths.
            return $"TentacleIT-{Guid.NewGuid():N}".ToLower();
        }

        async Task RunTentacleCommand(string tentacleExe, string[] args, TemporaryDirectory tmp, ILogger logger, CancellationToken cancellationToken)
        {
            await RunCommandOutOfProcess(
                tentacleExe,
                args, 
                "Tentacle",
                tmp, 
                _ => { }, 
                runTentacleEnvironmentVariables,
                logger,
                cancellationToken);
        }

        async Task RunCommandOutOfProcess(
            string targetFilePath,
            string[] args,
            string commandName,
            TemporaryDirectory tmp,
            Action<string> commandOutput, 
            IReadOnlyDictionary<string, string> environmentVariables,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            async Task ProcessLogs(string s, CancellationToken ct)
            {
                await Task.CompletedTask;
                logger.Information($"[{commandName}] " + s);
                commandOutput(s);
            }

            try
            {
                var commandResult = await RetryHelper.RetryAsync<CommandResult, CommandExecutionException>(
                    () => Cli.Wrap(targetFilePath)
                        .WithArguments(args)
                        .WithEnvironmentVariables(environmentVariables)
                        .WithWorkingDirectory(tmp.DirectoryPath)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(ProcessLogs))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(ProcessLogs))
                        .ExecuteAsync(cancellationToken));

                if (cancellationToken.IsCancellationRequested) return;

                if (commandResult.ExitCode != 0)
                {
                    throw new Exception($"{commandName} returns non zero exit code: " + commandResult.ExitCode);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
