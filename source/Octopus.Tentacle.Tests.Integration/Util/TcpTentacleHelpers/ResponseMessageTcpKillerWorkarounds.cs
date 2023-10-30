using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
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
    }
}
