using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public static class PendingRequestQueueWorkarounds
    {
        public static async Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles(this IAsyncClientScriptServiceV2 service, ILogger logger)
        {
            logger.Log().Information("Call GetStatus to work around an issue where the polling queue will send work to a disconnected tentacle");
            await DoIgnoringException(async () => await service.GetStatusAsync(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0), ProxyRequestOptions()));
            logger.Log().Information("Finished GetStatus work around call");
        }

        private static HalibutProxyRequestOptions ProxyRequestOptions()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            return new HalibutProxyRequestOptions(cts.Token, null);
        }

        public static async Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles(this IAsyncClientCapabilitiesServiceV2 service, ILogger logger)
        {
            logger.Log().Information("Call GetCapabilities to work around an issue where the polling queue will send work to a disconnected tentacle");
            await DoIgnoringException(async () => await service.GetCapabilitiesAsync(ProxyRequestOptions()));
            logger.Log().Information("Finished GetCapabilities work around call");
        }

        public static async Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles(this IAsyncClientFileTransferService service, ILogger logger)
        {
            logger.Log().Information("Call DownloadFile to work around an issue where the polling queue will send work to a disconnected tentacle");
            await DoIgnoringException(async () => await service.DownloadFileAsync("nope", ProxyRequestOptions()));
            logger.Log().Information("Finished DownloadFile work around call");
        }

        private static ILogger Log(this ILogger logger)
        {
            return logger.ForContext(typeof(PendingRequestQueueWorkarounds));
        }
        
        private static async Task DoIgnoringException(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception)
            {
            }
        }
    }
}