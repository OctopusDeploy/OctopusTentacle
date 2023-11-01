using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public static class ResponseMessageTcpKillerWorkarounds
    {
        public static async Task EnsureTentacleIsConnectedToServer(this IAsyncClientScriptService service, ILogger logger)
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call GetStatus to work around an issue where the tcp killer kills setup of new connections");
            await service.GetStatusAsync(new ScriptStatusRequest(new ScriptTicket("nope"), 0), new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished GetStatus work around call");
        }

        public static void EnsureTentacleIsConnectedToServer(this IClientScriptServiceV2 service, ILogger logger)
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call GetStatus to work around an issue where the tcp killer kills setup of new connections");
            service.GetStatus(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0), new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished GetStatus work around call");
        }

        public static async Task EnsureTentacleIsConnectedToServer(this IAsyncClientScriptServiceV2 service, ILogger logger)
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call GetStatus to work around an issue where the tcp killer kills setup of new connections");
            await service.GetStatusAsync(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0), new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished GetStatus work around call");
        }

        public static async Task EnsureTentacleIsConnectedToServer(this IAsyncClientFileTransferService service, ILogger logger)
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call DownloadFile to work around an issue where the tcp killer kills setup of new connections");
            await service.DownloadFileAsync("nope", new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished DownloadFile work around call");
        }

        public static async Task EnsureTentacleIsConnectedToServer(this ClientAndTentacle clientAndTentacle, SyncOrAsyncHalibut syncOrAsyncHalibut, ILogger logger)
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
    }
}
