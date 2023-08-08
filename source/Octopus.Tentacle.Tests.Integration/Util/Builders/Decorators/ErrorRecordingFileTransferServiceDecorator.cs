using System;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class FileTransferServiceExceptions
    {
        public Exception? UploadLatestException { get; set; }
        public Exception? DownloadFileLatestException { get; set; }
    }

    public class ErrorRecordingFileTransferServiceDecorator : IAsyncClientFileTransferService
    {
        private FileTransferServiceExceptions errors;
        private IAsyncClientFileTransferService inner;
        private ILogger logger;

        public ErrorRecordingFileTransferServiceDecorator(IAsyncClientFileTransferService inner, FileTransferServiceExceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingFileTransferServiceDecorator>();
        }

        public async Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.UploadFileAsync(remotePath, upload, options);
            }
            catch (Exception e)
            {
                errors.UploadLatestException = e;
                logger.Information(e, "Recorded error from UploadFile");
                throw;
            }
        }

        public async Task<DataStream> DownloadFileAsync(string remotePath, HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.DownloadFileAsync(remotePath, options);
            }
            catch (Exception e)
            {
                errors.DownloadFileLatestException = e;
                logger.Information(e, "Recorded error from DownloadFile");
                throw;
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderErrorRecordingFileTransferServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder RecordExceptionThrownInFileTransferService(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out FileTransferServiceExceptions fileTransferServiceExceptions)
        {
            var myFileTransferServiceExceptions = new FileTransferServiceExceptions();
            fileTransferServiceExceptions = myFileTransferServiceExceptions;
            return tentacleServiceDecoratorBuilder.DecorateFileTransferServiceWith(inner => new ErrorRecordingFileTransferServiceDecorator(inner, myFileTransferServiceExceptions));
        }
    }
}