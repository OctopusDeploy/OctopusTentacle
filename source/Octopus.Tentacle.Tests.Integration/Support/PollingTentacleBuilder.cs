using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder
    {
        readonly int octopusHalibutPort;
        string? octopusThumbprint;
        Uri? tentaclePollSubscriptionId;

        public PollingTentacleBuilder(int octopusHalibutPort)
        {
            this.octopusHalibutPort = octopusHalibutPort;
        }

        public PollingTentacleBuilder WithOctopusThumbprint(string octopusThumbprint)
        {
            this.octopusThumbprint = octopusThumbprint;
            return this;
        }

        public PollingTentacleBuilder WithTentaclePollSubscription(Uri tentaclePollSubscriptionId)
        {
            this.tentaclePollSubscriptionId = tentaclePollSubscriptionId;
            return this;
        }
        
        public (IDisposable, Task) Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");

            CreateInstance(configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(configFilePath, instanceName, Certificates.TentaclePfxPath, tempDirectory, cancellationToken);
            ConfigureTentacleToPollOctopusServer(
                configFilePath,
                octopusHalibutPort,
                octopusThumbprint ?? Certificates.ServerPublicThumbprint,
                tentaclePollSubscriptionId ?? PollingSubscriptionId.Generate());

            return (tempDirectory, RunningTentacle(configFilePath, instanceName, tempDirectory, cancellationToken));
        }

        private Task RunningTentacle(string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    RunTentacleCommand(new[]
                    {
                        "agent", "--config", configFilePath,
                        // Maybe it is looking in the wrong spot?
                        $"--instance={instanceName}",
                        "--noninteractive"
                    }, tmp, cancellationToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }, cancellationToken);
        }

        private void ConfigureTentacleToPollOctopusServer(string configFilePath, int octopusHalibutPort, string octopusThumbprint, Uri tentaclePollSubscriptionId)
        {
            // TODO: No, listen: NoListen
            //RunTentacleCommandInProcess("poll-server", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}");

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

        private void AddCertificateToTentacle(string configFilePath, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(new[]
            {
                "import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath,
                $"--instance={instanceName}"
            }, tmp, cancellationToken);
        }

        private void CreateInstance(string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(new[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void RunTentacleCommand(string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommandOutOfProcess(args, tmp, cancellationToken);
        }

        private void RunTentacleCommandOutOfProcess(string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            var tentacleExe = FindTentacleExe();

            var arguments = String.Join(" ", args);

            var exitCode = SilentProcessRunner.ExecuteCommand(
                tentacleExe,
                arguments,
                tmp.DirectoryPath,
                TestContext.WriteLine,
                TestContext.WriteLine,
                TestContext.WriteLine,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;
            
            if (exitCode != 0)
            {
                throw new Exception("Error running tentacle");
            }
        }

        private string FindTentacleExe()
        {
            var assemblyDirectory = Path.GetDirectoryName(GetType().Assembly.Location);

            // TODO this wont work locally with nuke.
            var tentacleExe = Path.Combine(assemblyDirectory, "Tentacle");

            // Are we on teamcity?
            // It seems no environment variables are passed to us.
            if (TestExecutionContext.IsRunningInTeamCity || assemblyDirectory.Contains("Team"))
            {
                // Example current directory of assembly.
                // /opt/TeamCity/BuildAgent/work/639265b01610d682/build/outputs/integrationtests/net6.0/linux-x64
                // Desired path to tentacle.
                // /opt/TeamCity/BuildAgent/work/639265b01610d682/build/outputs/tentaclereal/tentacle/Tentacle

                // TODO add exe
                tentacleExe = Path.Combine(Directory.GetParent(assemblyDirectory).Parent.Parent.FullName, "tentaclereal", "tentacle", "Tentacle");
            }

            if (PlatformDetection.IsRunningOnWindows) tentacleExe += ".exe";
            return tentacleExe;
        }

        public static class TestExecutionContext
        {
            public static bool IsRunningInTeamCity
            {
                get
                {
                    var teamcityenvvars = new String[] {"TEAMCITY_VERSION", "TEAMCITY_BUILD_ID"};
                    foreach (var s in teamcityenvvars)
                    {
                        var environmentVariableValue = Environment.GetEnvironmentVariable(s);
                        TestContext.WriteLine($"Env value: {s} is '{environmentVariableValue}'");
                        if (!string.IsNullOrEmpty(environmentVariableValue)) return true;
                    }

                    return false;
                }
            }
        }
    }
}
