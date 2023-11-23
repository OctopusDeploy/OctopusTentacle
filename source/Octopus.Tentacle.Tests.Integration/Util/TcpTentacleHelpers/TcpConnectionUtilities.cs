using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    class TcpConnectionUtilities : ITcpConnectionUtilities
    {
        readonly ILogger logger;
        IHalibutRuntime halibutRuntime;
        ServiceEndPoint serviceEndPoint;

        public TcpConnectionUtilities(ILogger logger)
        {
            this.logger = logger.ForContext<TcpConnectionUtilities>();
        }

        public void Configure(IHalibutRuntime halibutRuntime, ServiceEndPoint serviceEndPoint)
        {
            this.halibutRuntime = halibutRuntime;
            this.serviceEndPoint = serviceEndPoint;
        }

        public async Task RestartTcpConnection()
        {
            logger.Information("Call DownloadFile to work around an issue where the tcp killer kills setup of new connections");
            await ExecuteDownloadFile(new HalibutProxyRequestOptions(CancellationToken.None));
            logger.Information("Finished DownloadFile work around call");
        }

        public async Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles()
        {
            logger.Information("Call DownloadFile to work around an issue where the polling queue will send work to a disconnected tentacle");
            try
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(500));

                await ExecuteDownloadFile(new HalibutProxyRequestOptions(cts.Token));
            }
            catch
            {
                //We don't care about any exceptions, so just swallow them up
            }

            logger.Information("Finished DownloadFile work around call");
        }

        async Task ExecuteDownloadFile(HalibutProxyRequestOptions proxyRequestOptions)
        {
            var asyncFileTransferService = halibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(serviceEndPoint);
            await asyncFileTransferService.DownloadFileAsync("nope", proxyRequestOptions);
        }
    }

    public interface ITcpConnectionUtilities
    {
        Task RestartTcpConnection();
        Task EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
    }
}