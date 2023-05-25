using Halibut;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToFileTransferServiceDecorator : IFileTransferService
    {
        private IFileTransferService inner;
        private ILogger logger;

        public LogCallsToFileTransferServiceDecorator(IFileTransferService inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToFileTransferServiceDecorator>();
        }

        public UploadResult UploadFile(string remotePath, DataStream upload)
        {
            logger.Information("UploadFile call started");
            try
            {
                return inner.UploadFile(remotePath, upload);
            }
            finally
            {
                logger.Information("UploadFile call complete");
            }
        }

        public DataStream DownloadFile(string remotePath)
        {
            logger.Information("DownloadFile call started");
            try
            {
                return inner.DownloadFile(remotePath);
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