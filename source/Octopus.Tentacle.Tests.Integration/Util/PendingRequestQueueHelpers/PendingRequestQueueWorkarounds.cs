using System;
using System.Threading;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public static class PendingRequestQueueWorkarounds
    {
        public static void EnsurePollingQueueWontSendMessageToDisconnectedTentacles(this IClientScriptServiceV2 service, ILogger logger)
        {
            logger.Log().Information("Call GetStatus to work around an issue where the polling queue will send work to a disconnected tentacle");
            DoIgnoringException(() => service.GetStatus(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0), ProxyRequestOptions()));
            logger.Log().Information("Finished GetStatus work around call");
        }

        private static HalibutProxyRequestOptions ProxyRequestOptions()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            return new HalibutProxyRequestOptions(cts.Token);
        }

        public static void EnsurePollingQueueWontSendMessageToDisconnectedTentacles(this IClientCapabilitiesServiceV2 service, ILogger logger)
        {
            logger.Log().Information("Call GetCapabilities to work around an issue where the polling queue will send work to a disconnected tentacle");
            DoIgnoringException(() => service.GetCapabilities(ProxyRequestOptions()));
            logger.Log().Information("Finished GetCapabilities work around call");
        }

        public static void EnsurePollingQueueWontSendMessageToDisconnectedTentacles(this IClientFileTransferService service, ILogger logger)
        {
            logger.Log().Information("Call DownloadFile to work around an issue where the polling queue will send work to a disconnected tentacle");
            DoIgnoringException(() => service.DownloadFile("nope", ProxyRequestOptions()));
            logger.Log().Information("Finished DownloadFile work around call");
        }

        private static ILogger Log(this ILogger logger)
        {
            return logger.ForContext(typeof(PendingRequestQueueWorkarounds));
        }
        
        private static void DoIgnoringException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
            }
        }
    }
}