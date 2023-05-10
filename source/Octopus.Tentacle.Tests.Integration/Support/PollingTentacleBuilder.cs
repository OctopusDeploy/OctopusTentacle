using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Nito.AsyncEx.Interop;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder
    {
        readonly int octopusHalibutPort;
        readonly string octopusThumbprint;
        Uri? tentaclePollSubscriptionId;
        private string? tentacleExePath;

        private string? CertificatePfxPath;
        private string? TentacleThumbprint;

        public PollingTentacleBuilder(int octopusHalibutPort, string octopusThumbprint)
        {
            this.octopusHalibutPort = octopusHalibutPort;
            this.octopusThumbprint = octopusThumbprint;
        }

        public PollingTentacleBuilder WithTentaclePollSubscription(Uri tentaclePollSubscriptionId)
        {
            this.tentaclePollSubscriptionId = tentaclePollSubscriptionId;
            return this;
        }

        public PollingTentacleBuilder WithCertificate(string CertificatePfxPath, string TentacleThumbprint)
        {
            this.CertificatePfxPath = CertificatePfxPath;
            this.TentacleThumbprint = TentacleThumbprint;
            return this;
        }

        public PollingTentacleBuilder WithTentacleExe(string tentacleExe)
        {
            tentacleExePath = tentacleExe;
            return this;
        }

        internal async Task<RunningTestTentacle> Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = tentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            var subscriptionId = tentaclePollSubscriptionId ?? PollingSubscriptionId.Generate();
            var CertificatePfxPath = this.CertificatePfxPath ?? Certificates.TentaclePfxPath;
            var tentacleThumbprint = TentacleThumbprint ?? Certificates.TentaclePublicThumbprint;
            CreateInstance(tentacleExe, configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(tentacleExe, configFilePath, instanceName, CertificatePfxPath, tempDirectory, cancellationToken);
            ConfigureTentacleToPollOctopusServer(
                configFilePath,
                octopusHalibutPort,
                octopusThumbprint ?? Certificates.ServerPublicThumbprint,
                subscriptionId);

            Func<CancellationToken, Task<Task>> startTentacle = token => RunningTentacle(tentacleExe, configFilePath, instanceName, tempDirectory, token);  
            var runningTentacle =  new RunningTestTentacle(subscriptionId, tempDirectory, startTentacle, tentacleThumbprint);
            try
            {
                await runningTentacle.Start(cancellationToken);
                return runningTentacle;
            }
            catch (Exception)
            {
                try
                {
                    runningTentacle.Dispose();
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(e);
                }

                // Throw the interesting exception
                throw;
            }
        }

        private async Task<Task> RunningTentacle(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();

            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    RunTentacleCommandOutOfProcess(tentacleExe, new[] {"agent", "--config", configFilePath, $"--instance={instanceName}", "--noninteractive"}, tmp,
                        s =>
                        {
                            if (s.Contains("Agent will not listen") || s.Contains("Agent listening on"))
                            {
                                hasTentacleStarted.Set();
                            }
                        }, cancellationToken);
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(e);
                    throw;
                }
            }, cancellationToken);

            await Task.WhenAny(runningTentacle, WaitHandleAsyncFactory.FromWaitHandle(hasTentacleStarted.WaitHandle, TimeSpan.FromMinutes(1)));

            // Will throw.
            if (runningTentacle.IsCompleted) await runningTentacle;

            if (!hasTentacleStarted.IsSet)
            {
                throw new Exception("Tentacle did not appear to start correctly");
            }

            return runningTentacle;
        }

        private void ConfigureTentacleToPollOctopusServer(string configFilePath, int octopusHalibutPort, string octopusThumbprint, Uri tentaclePollSubscriptionId)
        {
            var startUpConfigFileInstanceRequest = new StartUpConfigFileInstanceRequest(configFilePath);
            using var container = new Program(new string[] { }).BuildContainer(startUpConfigFileInstanceRequest);

            var writableTentacleConfiguration = container.Resolve<IWritableTentacleConfiguration>();
            writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(octopusThumbprint)
            {
                Address = new Uri("https://localhost:" + octopusHalibutPort),
                CommunicationStyle = CommunicationStyle.TentacleActive,
                SubscriptionId = tentaclePollSubscriptionId.ToString()
            });

            writableTentacleConfiguration.SetNoListen(true);
        }

        private void AddCertificateToTentacle(string tentacleExe, string configFilePath, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] {"import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void CreateInstance(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void RunTentacleCommand(string tentacleExe, string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommandOutOfProcess(tentacleExe, args, tmp, s =>
            {
            }, cancellationToken);
        }

        private void RunTentacleCommandOutOfProcess(string tentacleExe,
            string[] args,
            TemporaryDirectory tmp,
            Action<string> CommandOutput,
            CancellationToken cancellationToken)
        {
            Action<string> allOutput = s =>
            {
                TestContext.WriteLine(s);
                CommandOutput(s);
            };
            var exitCode = SilentProcessRunner.ExecuteCommand(
                tentacleExe,
                String.Join(" ", args),
                tmp.DirectoryPath,
                allOutput,
                allOutput,
                allOutput,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (exitCode != 0)
            {
                throw new Exception("Tentacle returns non zero exit code: " + exitCode);
            }
        }
    }

    public class RunningTestTentacle : IDisposable
    {
        public Uri ServiceUri { get; }
        private readonly IDisposable TemporaryDirectory;
        private CancellationTokenSource? cts;
        private Task? RunningTentacleTask { get; set; }
        private Func<CancellationToken, Task<Task>> startTentacle;
        public string Thumbprint { get; }

        public RunningTestTentacle(Uri serviceUri, 
            IDisposable temporaryDirectory,
            Func<CancellationToken, Task<Task>> startTentacle
            , string thumbprint)
        {
            TemporaryDirectory = temporaryDirectory;
            this.startTentacle = startTentacle;
            Thumbprint = thumbprint;
            ServiceUri = serviceUri;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (RunningTentacleTask != null) throw new Exception("Tentacle is already running, call stop() first");

            cts = new CancellationTokenSource();

            RunningTentacleTask = await startTentacle(cts.Token);
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }

            var t = RunningTentacleTask;
            RunningTentacleTask = null;
            await t;
        }

        public void Dispose()
        {
            if (RunningTentacleTask != null) Stop(CancellationToken.None).GetAwaiter().GetResult();
            TemporaryDirectory.Dispose();
        }
    }
}