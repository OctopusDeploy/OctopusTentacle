using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacle: IAsyncDisposable
    {
        readonly IHalibutRuntime halibutRuntime;
        readonly ILogger logger;

        public ServiceEndPoint ServiceEndPoint { get; }
        public Server Server { get; }
        public PortForwarder? PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public TentacleClient TentacleClient { get; }
        public TemporaryDirectory TemporaryDirectory { get; }
        public RpcRetrySettings RpcRetrySettings { get; }

        public LegacyTentacleClientBuilder LegacyTentacleClientBuilder()
        {
            return new LegacyTentacleClientBuilder(halibutRuntime, ServiceEndPoint);
        }

        // The integration test for AbandonScript needs to call AbandonScriptAsync directly
        // over the wire to assert on the RPC response shape and to drive the cancel→abandon
        // sequence without going through TentacleClient's higher-level ExecuteScript orchestrator.
        // TentacleClient deliberately doesn't expose AbandonScript at all today; the server is
        // the only production consumer, and it talks to the Halibut client directly too.
        // Exposing a direct client here keeps the test focused on the RPC behavior.
        public IAsyncClientScriptServiceV2 CreateScriptServiceV2Client()
        {
            return halibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(ServiceEndPoint);
        }

        public ClientAndTentacle(IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint,
            Server server,
            PortForwarder? portForwarder,
            RunningTentacle runningTentacle,
            TentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory, 
            RpcRetrySettings rpcRetrySettings,
            ILogger logger)
        {
            this.halibutRuntime = halibutRuntime;
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
            TemporaryDirectory = temporaryDirectory;
            RpcRetrySettings = rpcRetrySettings;
            this.ServiceEndPoint = serviceEndPoint;
            this.logger = logger.ForContext<ClientAndTentacle>();
        }

        public async ValueTask DisposeAsync()
        {
            SafelyMoveTentacleLogFileToSharedLocation();

            var banner = new StringBuilder();
            banner.AppendLine("");
            banner.AppendLine("");

            banner.AppendLine("****** ****** ****** ****** ****** ****** ******");
            banner.AppendLine("****** CLIENT AND TENTACLE DISPOSE CALLED  *****");
            banner.AppendLine("*     Subsequent errors should be ignored      *");
            banner.AppendLine("****** ****** ****** ****** ****** ****** ******");

            logger.Information(banner.ToString());

            logger.Information("Starting DisposeAsync");

            logger.Information("Starting RunningTentacle.DisposeAsync and Server.Dispose and PortForwarder.Dispose");
            var portForwarderTask = Task.Run(() => PortForwarder?.Dispose());
            var runningTentacleTask = RunningTentacle.DisposeAsync();
            var serverTask = Server.DisposeAsync();
            await Task.WhenAll(runningTentacleTask.AsTask(), serverTask.AsTask(), portForwarderTask);

            logger.Information("Starting TentacleClient.Dispose");
            TentacleClient.Dispose();
            logger.Information("Starting TemporaryDirectory.Dispose");
            TemporaryDirectory.Dispose();
            logger.Information("Finished DisposeAsync");
        }

        void SafelyMoveTentacleLogFileToSharedLocation()
        {
            try
            {
                var logFilePath = RunningTentacle.LogFilePath;
                var destinationFilePath = IntegrationTest.GetTempTentacleLogPath();

                File.Move(logFilePath, destinationFilePath);
            }
            catch (Exception e)
            {
                logger.Warning(e, "Failed to move the Tentacle log file on Disposal");
            }
        }
    }
}