using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class FileTransferServiceCallCounts
    {
        public long UploadFileCallCountStarted;
        public long DownloadFileCallCountStarted;

        public long UploadFileCallCountComplete;
        public long DownloadFileCallCountComplete;
    }

    public class CountingCallsFileTransferServiceDecorator : IAsyncClientFileTransferService
    {
        private readonly FileTransferServiceCallCounts counts;

        private readonly IAsyncClientFileTransferService inner;

        public CountingCallsFileTransferServiceDecorator(IAsyncClientFileTransferService inner, FileTransferServiceCallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public async Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.UploadFileCallCountStarted);
            try
            {
                return await inner.UploadFileAsync(remotePath, upload, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.UploadFileCallCountComplete);
            }
        }

        public async Task<DataStream> DownloadFileAsync(string remotePath, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.DownloadFileCallCountStarted);
            try
            {
                return await inner.DownloadFileAsync(remotePath, options);
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