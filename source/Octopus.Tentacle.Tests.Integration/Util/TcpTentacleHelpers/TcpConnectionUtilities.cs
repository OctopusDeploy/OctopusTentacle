using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    class TcpConnectionUtilities : ITcpConnectionUtilities
    {
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly ILogger logger;

        public IHalibutRuntime HalibutRuntime { get; set; }
        public ServiceEndPoint ServiceEndPoint { get; set; }

        public TcpConnectionUtilities(AsyncHalibutFeature asyncHalibutFeature, ILogger logger)
        {
            this.asyncHalibutFeature = asyncHalibutFeature;
            this.logger = logger.ForContext<TcpConnectionUtilities>();
        }

        public async Task RestartTcpConnection()
        {
            logger.Information("Call DownloadFile to work around an issue where the tcp killer kills setup of new connections");
            await ExecuteDownloadFile(new HalibutProxyRequestOptions(CancellationToken.None, CancellationToken.None));
            logger.Information("Finished DownloadFile work around call");
        }

        private static HalibutProxyRequestOptions PollingQueueProxyRequestOptions()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            return new HalibutProxyRequestOptions(cts.Token, null);
        }

        public async Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles()
        {
            logger.Information("Call DownloadFile to work around an issue where the polling queue will send work to a disconnected tentacle");
            try
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(500));

                await ExecuteDownloadFile(new HalibutProxyRequestOptions(cts.Token, null));
            }
            catch
            {
                //We don't care about any exceptions, so just swallow them up
            }

            logger.Information("Finished DownloadFile work around call");
        }

        async Task ExecuteDownloadFile(HalibutProxyRequestOptions proxyRequestOptions)
        {
            if (asyncHalibutFeature == AsyncHalibutFeature.Disabled)
            {
#pragma warning disable CS0612
                var syncFileTransferService = HalibutRuntime.CreateClient<IFileTransferService, IClientFileTransferService>(ServiceEndPoint);
#pragma warning restore CS0612

                syncFileTransferService.DownloadFile("nope", proxyRequestOptions);
            }
            else
            {
                var asyncFileTransferService = HalibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(ServiceEndPoint);

                await asyncFileTransferService.DownloadFileAsync("nope", proxyRequestOptions);
            }
        }
    }

    public interface ITcpConnectionUtilities
    {
        Task RestartTcpConnection();
        Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
    }
}