using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public interface ITcpConnectionFixer
    {
        Task EnsureTentacleIsConnectedToServer();
    }

    public class TcpConnectionFixer : ITcpConnectionFixer
    {
        readonly ClientAndTentacle clientAndTentacle;
        readonly SyncOrAsyncHalibut syncOrAsyncHalibut;
        readonly ILogger logger;

        public static readonly ITcpConnectionFixer None = new NoopTcpConnectionFixer();

        public TcpConnectionFixer(ClientAndTentacle clientAndTentacle, SyncOrAsyncHalibut syncOrAsyncHalibut, ILogger logger)
        {
            this.clientAndTentacle = clientAndTentacle;
            this.syncOrAsyncHalibut = syncOrAsyncHalibut;
            this.logger = logger;
        }

        public async Task EnsureTentacleIsConnectedToServer()
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call DownloadFile to work around an issue where the tcp killer kills setup of new connections");
            if (syncOrAsyncHalibut == SyncOrAsyncHalibut.Sync)
            {
#pragma warning disable CS0612
                var syncFileTransferService = clientAndTentacle.Server.ServerHalibutRuntime.CreateClient<IFileTransferService, IClientFileTransferService>(clientAndTentacle.ServiceEndPoint);
#pragma warning restore CS0612

                syncFileTransferService.DownloadFile("nope", new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            }
            else
            {
                var asyncFileTransferService = clientAndTentacle.Server.ServerHalibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(clientAndTentacle.ServiceEndPoint);

                await asyncFileTransferService.DownloadFileAsync("nope", new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            }

            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished DownloadFile work around call");
        }

        class NoopTcpConnectionFixer: ITcpConnectionFixer
        {
            public Task EnsureTentacleIsConnectedToServer() => Task.CompletedTask;
        }
    }

    public static class TcpConnectionFixerExtensions
    {
        public static ITcpConnectionFixer GetTcpConnectionFixer(this ClientAndTentacle clientAndTentacle, SyncOrAsyncHalibut syncOrAsyncHalibut, ILogger logger)
        {
            return new TcpConnectionFixer(clientAndTentacle, syncOrAsyncHalibut, logger);
        }
    }
}