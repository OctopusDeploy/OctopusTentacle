using Serilog;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public static class ResponseMessageTcpKillerWorkarounds
    {
        public static void EnsureTentacleIsConnectedToServer(this IScriptServiceV2 service, ILogger logger)
        {
            logger.Information("Call GetStatus to work around an issue where the tcp killer kills setup of new connections");
            service.GetStatus(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0));
            logger.Information("Finished GetStatus work around call");
        }

        public static void EnsureTentacleIsConnectedToServer(this ICapabilitiesServiceV2 service, ILogger logger)
        {
            logger.Information("Call GetCapabilities to work around an issue where the tcp killer kills setup of new connections");
            service.GetCapabilities();
            logger.Information("Finished GetCapabilities work around call");
        }

        public static void EnsureTentacleIsConnectedToServer(this IFileTransferService service, ILogger logger)
        {
            logger.Information("Call DownloadFile to work around an issue where the tcp killer kills setup of new connections");
            service.DownloadFile("nope");
            logger.Information("Finished DownloadFile work around call");
        }
    }
}
