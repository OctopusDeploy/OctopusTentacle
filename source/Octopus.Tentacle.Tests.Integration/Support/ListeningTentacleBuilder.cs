using System;
using System.IO;
using System.Text.RegularExpressions;
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
    public class ListeningTentacleBuilder
    {
        readonly string octopusThumbprint;
        private string? tentacleExePath;

        private string? CertificatePfxPath;
        private string? TentacleThumbprint;

        public ListeningTentacleBuilder(string octopusThumbprint)
        {
            this.octopusThumbprint = octopusThumbprint;
        }

        public ListeningTentacleBuilder WithCertificate(string CertificatePfxPath, string TentacleThumbprint)
        {
            this.CertificatePfxPath = CertificatePfxPath;
            this.TentacleThumbprint = TentacleThumbprint;
            return this;
        }

        public ListeningTentacleBuilder WithTentacleExe(string tentacleExe)
        {
            tentacleExePath = tentacleExe;
            return this;
        }

        internal async Task<RunningTestListeningTentacle> Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = tentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            var CertificatePfxPath = this.CertificatePfxPath ?? Certificates.TentaclePfxPath;
            var tentacleThumbprint = TentacleThumbprint ?? Certificates.TentaclePublicThumbprint;
            CreateInstance(tentacleExe, configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(tentacleExe, configFilePath, instanceName, CertificatePfxPath, tempDirectory, cancellationToken);
            ConfigureTentacleToListen(configFilePath, octopusThumbprint ?? Certificates.ServerPublicThumbprint);

            Func<CancellationToken, Task<(string, Task)>> startTentacle = token => RunningTentacle(tentacleExe, configFilePath, instanceName, tempDirectory, token);
            var runningTentacle =  new RunningTestListeningTentacle(tempDirectory, startTentacle, tentacleThumbprint);
            
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

        private static Regex listeningPortRegex = new Regex(@"^listen:\/\/.+:(\d+)\/");

        private async Task<(string, Task)> RunningTentacle(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();

            string listeningPort = null;

            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    RunTentacleCommandOutOfProcess(tentacleExe, new[] {"agent", $"--instance={instanceName}", "--noninteractive"}, tmp,
                        s =>
                        {
                            if (s.Contains("Agent will not listen") || s.Contains("Agent listening on"))
                            {
                                hasTentacleStarted.Set();
                            }
                            else if (s.Contains("Listener started"))
                            {
                                listeningPort = listeningPortRegex.Match(s).Groups[1].Value;
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

            return (listeningPort, runningTentacle);
        }

        private void ConfigureTentacleToListen(string configFilePath, string octopusThumbprint)
        {
            var startUpConfigFileInstanceRequest = new StartUpConfigFileInstanceRequest(configFilePath);
            using var container = new Program(new string[] { }).BuildContainer(startUpConfigFileInstanceRequest);

            var writableTentacleConfiguration = container.Resolve<IWritableTentacleConfiguration>();
            writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(octopusThumbprint)
            {
                CommunicationStyle = CommunicationStyle.TentaclePassive,
            });
            writableTentacleConfiguration.SetApplicationDirectory(Path.Combine(new DirectoryInfo(configFilePath).Parent.FullName, "appdir"));
            writableTentacleConfiguration.SetServicesPortNumber(0); // Find a random available port
            writableTentacleConfiguration.SetNoListen(false);
        }

        private void AddCertificateToTentacle(string tentacleExe, string configFilePath, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] {"import-certificate", $"--from-file={tentaclePfxPath}", $"--instance={instanceName}"}, tmp, cancellationToken);
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

    class RunningTestListeningTentacle : IDisposable
    {
        private readonly IDisposable TemporaryDirectory;
        private CancellationTokenSource cts;
        private Task? RunningTentacleTask { get; set; }
        private Func<CancellationToken, Task<(string, Task)>> startTentacle;
        public string Thumbprint { get; }
        public Uri ServiceUri { get; private set; }

        public RunningTestListeningTentacle( 
            IDisposable temporaryDirectory,
            Func<CancellationToken, Task<(string, Task)>> startTentacle,
            string thumbprint)
        {
            TemporaryDirectory = temporaryDirectory;
            this.startTentacle = startTentacle;
            Thumbprint = thumbprint;
        }
        
        public async Task Start(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (RunningTentacleTask != null) throw new Exception("Tentacle is already running, call stop() first");

            cts = new CancellationTokenSource();

            var (port, stopTask) = await startTentacle(cts.Token);
            RunningTentacleTask = stopTask;
            ServiceUri = new Uri($"https://localhost:{port}");
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
