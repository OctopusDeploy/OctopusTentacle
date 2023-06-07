using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToFileTransferServiceDecorator : IClientFileTransferService
    {
        private IClientFileTransferService inner;
        private ILogger logger;

        public LogCallsToFileTransferServiceDecorator(IClientFileTransferService inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToFileTransferServiceDecorator>();
        }

        public UploadResult UploadFile(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
        {
            logger.Information("UploadFile call started");
            try
            {
                return inner.UploadFile(remotePath, upload, options);
            }
            finally
            {
                logger.Information("UploadFile call complete");
            }
        }

        public DataStream DownloadFile(string remotePath, HalibutProxyRequestOptions options)
        {
            logger.Information("DownloadFile call started");
            try
            {
                return inner.DownloadFile(remotePath, options);
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