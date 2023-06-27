using System.Threading;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Serilog;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public static class ResponseMessageTcpKillerWorkarounds
    {
        public static void EnsureTentacleIsConnectedToServer(this IClientScriptServiceV2 service, ILogger logger)
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call GetStatus to work around an issue where the tcp killer kills setup of new connections");
            service.GetStatus(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0), new HalibutProxyRequestOptions(CancellationToken.None));
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished GetStatus work around call");
        }

        public static void EnsureTentacleIsConnectedToServer(this IClientFileTransferService service, ILogger logger)
        {
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Call DownloadFile to work around an issue where the tcp killer kills setup of new connections");
            service.DownloadFile("nope", new HalibutProxyRequestOptions(CancellationToken.None));
            logger.ForContext(typeof(ResponseMessageTcpKillerWorkarounds)).Information("Finished DownloadFile work around call");
        }
    }
}
