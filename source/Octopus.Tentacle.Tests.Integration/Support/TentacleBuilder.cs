using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Nito.AsyncEx.Interop;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public abstract class TentacleBuilder<T> where T : TentacleBuilder<T>
    {
        protected string? ServerThumbprint;
        protected string? TentacleExePath;
        protected string CertificatePfxPath = Certificates.TentaclePfxPath;
        protected string TentacleThumbprint = Certificates.TentaclePublicThumbprint;
        private readonly Regex listeningPortRegex = new Regex(@"^listen:\/\/.+:(\d+)\/");

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

        internal IWritableTentacleConfiguration GetWritableTentacleConfiguration(string configFilePath)
        {
            var startUpConfigFileInstanceRequest = new StartUpConfigFileInstanceRequest(configFilePath);
            using var container = new Program(Array.Empty<string>()).BuildContainer(startUpConfigFileInstanceRequest);

            var writableTentacleConfiguration = container.Resolve<IWritableTentacleConfiguration>();
            return writableTentacleConfiguration;
        }

        internal async Task<RunningTentacle> StartTentacle(
            Uri? serviceUri,
            string tentacleExe,
            string instanceName,
            TemporaryDirectory tempDirectory,
            string tentacleThumbprint,
            CancellationToken cancellationToken)
        {
            Task<(Task, Uri?)> StartTentacleFunction(CancellationToken ct) => RunTentacle(serviceUri, tentacleExe, instanceName, tempDirectory, ct);

            var runningTentacle = new RunningTentacle(
                tempDirectory,
                StartTentacleFunction,
                tentacleThumbprint);

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

                throw;
            }
        }

        internal async Task<(Task task, Uri serviceUri)> RunTentacle(
            Uri? serviceUri,
            string tentacleExe,
            string instanceName,
            TemporaryDirectory tempDirectory,
            CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();
            int? listeningPort = null;

            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    RunTentacleCommandOutOfProcess(tentacleExe, new[] { "agent", $"--instance={instanceName}", "--noninteractive" }, tempDirectory,
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


        internal void AddCertificateToTentacle(string tentacleExe, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] { "import-certificate", $"--from-file={tentaclePfxPath}", $"--instance={instanceName}" }, tmp, cancellationToken);
        }

        internal void CreateInstance(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] { "create-instance", "--config", configFilePath, $"--instance={instanceName}" }, tmp, cancellationToken);
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
            Action<string> commandOutput,
            CancellationToken cancellationToken)
        {
            void AllOutput(string s)
            {
                TestContext.WriteLine(s);
                commandOutput(s);
            }

            var exitCode = SilentProcessRunner.ExecuteCommand(
                tentacleExe,
                string.Join(" ", args),
                tmp.DirectoryPath,
                AllOutput,
                AllOutput,
                AllOutput,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (exitCode != 0)
            {
                throw new Exception("Tentacle returns non zero exit code: " + exitCode);
            }
        }
    }

    public class RunningTentacle : IDisposable
    {
        private readonly IDisposable temporaryDirectory;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? runningTentacleTask;
        private readonly Func<CancellationToken, Task<(Task runningTentacleTask, Uri serviceUri)>> startTentacleFunction;

        public RunningTentacle(
            IDisposable temporaryDirectory,
            Func<CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            string thumbprint)
        {
            this.startTentacleFunction = startTentacleFunction;
            this.temporaryDirectory = temporaryDirectory;

            Thumbprint = thumbprint;
        }

        public Uri ServiceUri { get; private set; }
        public string Thumbprint { get; }

        public async Task Start(CancellationToken cancellationToken)
        {
            if (runningTentacleTask != null)
            {
                throw new Exception("Tentacle is already running, call stop() first");
            }

            cancellationTokenSource = new CancellationTokenSource();

            var (rtt, serviceUri) = await startTentacleFunction(cancellationTokenSource.Token);

            runningTentacleTask = rtt;
            ServiceUri = serviceUri;
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            var t = runningTentacleTask;
            runningTentacleTask = null;
            await t;
        }

        public void Dispose()
        {
            if (runningTentacleTask != null)
            {
                Stop(CancellationToken.None).GetAwaiter().GetResult();
            }

            temporaryDirectory.Dispose();
        }
    }
}