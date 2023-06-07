using System;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class FileTransferServiceExceptions
    {
        public Exception? UploadLatestException { get; set; }
        public Exception? DownloadFileLatestException { get; set; }
    }

    public class ErrorRecordingFileTransferServiceDecorator : IClientFileTransferService
    {
        private FileTransferServiceExceptions errors;
        private IClientFileTransferService inner;
        private ILogger logger;

        public ErrorRecordingFileTransferServiceDecorator(IClientFileTransferService inner, FileTransferServiceExceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingFileTransferServiceDecorator>();
        }

        public UploadResult UploadFile(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.UploadFile(remotePath, upload, options);
            }
            catch (Exception e)
            {
                errors.UploadLatestException = e;
                logger.Information(e, "Recorded error from UploadFile");
                throw;
            }
        }

        public DataStream DownloadFile(string remotePath, HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.DownloadFile(remotePath, options);
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