using System;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class FileTransferServiceDecoratorBuilder
    {
        public delegate UploadResult UploadFileClientDecorator(IClientFileTransferService inner, string remotePath, DataStream upload, HalibutProxyRequestOptions proxyRequestOptions); 
        public delegate DataStream DownloadFileClientDecorator(IClientFileTransferService inner, string remotePath, HalibutProxyRequestOptions proxyRequestOptions);
        
        private UploadFileClientDecorator uploadFileFunc = (inner, remotePath, upload, options) => inner.UploadFile(remotePath, upload, options);
        private DownloadFileClientDecorator downloadFileFunc = (inner, remotePath, options) => inner.DownloadFile(remotePath, options);

        public FileTransferServiceDecoratorBuilder BeforeUploadFile(Action beforeUploadFile)
        {
            return DecorateUploadFileWith((inner, remotePath, upload, options) =>
            {
                beforeUploadFile();
                return inner.UploadFile(remotePath, upload, options);
            });
        }

        public FileTransferServiceDecoratorBuilder BeforeUploadFile(Action<IClientFileTransferService, string, DataStream> beforeUploadFile)
        {
            return DecorateUploadFileWith((inner, remotePath, upload, options) =>
            {
                beforeUploadFile(inner, remotePath, upload);
                return inner.UploadFile(remotePath, upload, options);
            });
        }

        public FileTransferServiceDecoratorBuilder DecorateUploadFileWith(UploadFileClientDecorator uploadFileFunc)
        {
            this.uploadFileFunc = uploadFileFunc;
            return this;
        }

        public FileTransferServiceDecoratorBuilder BeforeDownloadFile(Action beforeDownloadFile)
        {
            return DecorateDownloadFileWith((inner, remotePath, options) =>
            {
                beforeDownloadFile();
                return inner.DownloadFile(remotePath, options);
            });
        }

        public FileTransferServiceDecoratorBuilder BeforeDownloadFile(Action<IClientFileTransferService, string> beforeDownloadFile)
        {
            return DecorateDownloadFileWith((inner, remotePath, options) =>
            {
                beforeDownloadFile(inner, remotePath);
                return inner.DownloadFile(remotePath, options);
            });
        }

        public FileTransferServiceDecoratorBuilder DecorateDownloadFileWith(DownloadFileClientDecorator downloadFileFunc)
        {
            this.downloadFileFunc = downloadFileFunc;
            return this;
        }

        public Func<IClientFileTransferService, IClientFileTransferService> Build()
        {
            return inner =>
            {
                return new FuncDecoratingFileTransferService(inner,
                    uploadFileFunc,
                    downloadFileFunc);
            };
        }


        private class FuncDecoratingFileTransferService : IClientFileTransferService
        {
            private IClientFileTransferService inner;
            private UploadFileClientDecorator uploadFileFunc;
            private DownloadFileClientDecorator downloadFileFunc;

            public FuncDecoratingFileTransferService(
                IClientFileTransferService inner,
                UploadFileClientDecorator uploadFileFunc,
                DownloadFileClientDecorator downloadFileFunc)
            {
                this.inner = inner;
                this.uploadFileFunc = uploadFileFunc;
                this.downloadFileFunc = downloadFileFunc;
            }

            public UploadResult UploadFile(string remotePath, DataStream upload, HalibutProxyRequestOptions options)
            {
                return uploadFileFunc(inner, remotePath, upload, options);
            }

            public DataStream DownloadFile(string remotePath, HalibutProxyRequestOptions options)
            {
                return downloadFileFunc(inner, remotePath, options);
            }
        }
    }

}