using System;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
{
    public class FileTransferServiceDecoratorBuilder
    {
        public delegate Task<UploadResult> UploadFileClientDecorator(IAsyncClientFileTransferService inner, string remotePath, DataStream upload, HalibutProxyRequestOptions proxyRequestOptions); 
        public delegate Task<DataStream> DownloadFileClientDecorator(IAsyncClientFileTransferService inner, string remotePath, HalibutProxyRequestOptions proxyRequestOptions);
        
        private UploadFileClientDecorator uploadFileFunc = async (inner, remotePath, upload, options) => await inner.UploadFileAsync(remotePath, upload, options);
        private DownloadFileClientDecorator downloadFileFunc = async (inner, remotePath, options) => await inner.DownloadFileAsync(remotePath, options);

        public FileTransferServiceDecoratorBuilder BeforeUploadFile(Func<Task>beforeUploadFile)
        {
            return DecorateUploadFileWith(async (inner, remotePath, upload, options) =>
            {
                await beforeUploadFile();
                return await inner.UploadFileAsync(remotePath, upload, options);
            });
        }

        public FileTransferServiceDecoratorBuilder BeforeUploadFile(Func<IAsyncClientFileTransferService, string, DataStream, Task> beforeUploadFile)
        {
            return DecorateUploadFileWith(async (inner, remotePath, upload, options) =>
            {
                await beforeUploadFile(inner, remotePath, upload);
                return await inner.UploadFileAsync(remotePath, upload, options);
            });
        }

        public FileTransferServiceDecoratorBuilder DecorateUploadFileWith(UploadFileClientDecorator uploadFileFunc)
        {
            this.uploadFileFunc = uploadFileFunc;
            return this;
        }

        public FileTransferServiceDecoratorBuilder BeforeDownloadFile(Func<Task> beforeDownloadFile)
        {
            return DecorateDownloadFileWith(async (inner, remotePath, options) =>
            {
                await beforeDownloadFile();
                return await inner.DownloadFileAsync(remotePath, options);
            });
        }

        public FileTransferServiceDecoratorBuilder BeforeDownloadFile(Func<IAsyncClientFileTransferService, string, Task> beforeDownloadFile)
        {
            return DecorateDownloadFileWith(async (inner, remotePath, options) =>
            {
                await beforeDownloadFile(inner, remotePath);
                return await inner.DownloadFileAsync(remotePath, options);
            });
        }

        public FileTransferServiceDecoratorBuilder DecorateDownloadFileWith(DownloadFileClientDecorator downloadFileFunc)
        {
            this.downloadFileFunc = downloadFileFunc;
            return this;
        }

        public Decorator<IAsyncClientFileTransferService> Build()
        {
            return inner =>
            {
                return new FuncDecoratingFileTransferService(inner,
                    uploadFileFunc,
                    downloadFileFunc);
            };
        }


        private class FuncDecoratingFileTransferService : IAsyncClientFileTransferService
        {
            private IAsyncClientFileTransferService inner;
            private UploadFileClientDecorator uploadFileFunc;
            private DownloadFileClientDecorator downloadFileFunc;

            public FuncDecoratingFileTransferService(
                IAsyncClientFileTransferService inner,
                UploadFileClientDecorator uploadFileFunc,
                DownloadFileClientDecorator downloadFileFunc)
            {
                this.inner = inner;
                this.uploadFileFunc = uploadFileFunc;
                this.downloadFileFunc = downloadFileFunc;
            }

            public async Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
            {
                return await uploadFileFunc(inner, remotePath, upload, options);
            }

            public async Task<DataStream> DownloadFileAsync(string remotePath, HalibutProxyRequestOptions options)
            {
                return await downloadFileFunc(inner, remotePath, options);
            }
        }
    }

}