using System.Threading;
using Halibut;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class FileTransferServiceCallCounts
    {
        public long UploadFileCallCountStarted;
        public long DownloadFileCallCountStarted;

        public long UploadFileCallCountComplete;
        public long DownloadFileCallCountComplete;
    }

    public class CountingCallsFileTransferServiceDecorator : IFileTransferService
    {
        private FileTransferServiceCallCounts counts;

        private IFileTransferService inner;

        public CountingCallsFileTransferServiceDecorator(IFileTransferService inner, FileTransferServiceCallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public UploadResult UploadFile(string remotePath, DataStream upload)
        {
            Interlocked.Increment(ref counts.UploadFileCallCountStarted);
            try
            {
                return inner.UploadFile(remotePath, upload);
            }
            finally
            {
                Interlocked.Increment(ref counts.UploadFileCallCountComplete);
            }
        }

        public DataStream DownloadFile(string remotePath)
        {
            Interlocked.Increment(ref counts.DownloadFileCallCountStarted);
            try
            {
                return inner.DownloadFile(remotePath);
            }
            finally
            {
                Interlocked.Increment(ref counts.DownloadFileCallCountComplete);
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderCountingCallsFileTransferServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder CountCallsToFileTransferService(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out FileTransferServiceCallCounts FileTransferServiceCallCounts)
        {
            var myFileTransferServiceCallCounts = new FileTransferServiceCallCounts();
            FileTransferServiceCallCounts = myFileTransferServiceCallCounts;
            return tentacleServiceDecoratorBuilder.DecorateFileTransferServiceWith(inner => new CountingCallsFileTransferServiceDecorator(inner, myFileTransferServiceCallCounts));
        }
    }
}