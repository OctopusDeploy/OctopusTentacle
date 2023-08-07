using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToFileTransferServiceDecorator : IAsyncClientFileTransferService
    {
        private IAsyncClientFileTransferService inner;
        private ILogger logger;

        public LogCallsToFileTransferServiceDecorator(IAsyncClientFileTransferService inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToFileTransferServiceDecorator>();
        }

        public async Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
        {
            logger.Information("UploadFile call started");
            try
            {
                return await inner.UploadFileAsync(remotePath, upload, options);
            }
            finally
            {
                logger.Information("UploadFile call complete");
            }
        }

        public async Task<DataStream> DownloadFileAsync(string remotePath, HalibutProxyRequestOptions options)
        {
            logger.Information("DownloadFile call started");
            try
            {
                return await inner.DownloadFileAsync(remotePath, options);
            }
            finally
            {
                logger.Information("DownloadFile call complete");
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderLogCallsToFileTransferServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder LogCallsToFileTransferService(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder)
        {
            return tentacleServiceDecoratorBuilder.DecorateFileTransferServiceWith(inner => new LogCallsToFileTransferServiceDecorator(inner));
        }
    }
}