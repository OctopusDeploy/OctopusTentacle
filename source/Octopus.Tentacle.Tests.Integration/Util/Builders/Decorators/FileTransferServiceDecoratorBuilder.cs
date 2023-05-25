using System;
using Halibut;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class FileTransferServiceDecoratorBuilder
    {
        private Func<IFileTransferService, string, DataStream, UploadResult> uploadFileFunc = (inner, remotePath, upload) => inner.UploadFile(remotePath, upload);
        private Func<IFileTransferService, string, DataStream> downloadFileFunc = (inner, remotePath) => inner.DownloadFile(remotePath);

        public FileTransferServiceDecoratorBuilder BeforeUploadFile(Action beforeUploadFile)
        {
            return DecorateUploadFileWith((inner, remotePath, upload) =>
            {
                beforeUploadFile();
                return inner.UploadFile(remotePath, upload);
            });
        }

        public FileTransferServiceDecoratorBuilder DecorateUploadFileWith(Func<IFileTransferService, string, DataStream, UploadResult> uploadFileFunc)
        {
            this.uploadFileFunc = uploadFileFunc;
            return this;
        }

        public FileTransferServiceDecoratorBuilder BeforeDownloadFile(Action beforeDownloadFile)
        {
            return DecorateDownloadFileWith((inner, remotePath) =>
            {
                beforeDownloadFile();
                return inner.DownloadFile(remotePath);
            });
        }

        public FileTransferServiceDecoratorBuilder DecorateDownloadFileWith(Func<IFileTransferService, string, DataStream> downloadFileFunc)
        {
            this.downloadFileFunc = downloadFileFunc;
            return this;
        }

        public Func<IFileTransferService, IFileTransferService> Build()
        {
            return inner =>
            {
                return new FuncDecoratingFileTransferService(inner,
                    uploadFileFunc,
                    downloadFileFunc);
            };
        }


        private class FuncDecoratingFileTransferService : IFileTransferService
        {
            private IFileTransferService inner;
            private Func<IFileTransferService, string, DataStream, UploadResult> uploadFileFunc;
            private Func<IFileTransferService, string, DataStream> downloadFileFunc;

            public FuncDecoratingFileTransferService(
                IFileTransferService inner,
                Func<IFileTransferService, string, DataStream, UploadResult> uploadFileFunc,
                Func<IFileTransferService, string, DataStream> downloadFileFunc)
            {
                this.inner = inner;
                this.uploadFileFunc = uploadFileFunc;
                this.downloadFileFunc = downloadFileFunc;
            }

            public UploadResult UploadFile(string remotePath, DataStream upload)
            {
                return uploadFileFunc(inner, remotePath, upload);
            }

            public DataStream DownloadFile(string remotePath)
            {
                return downloadFileFunc(inner, remotePath);
            }
        }
    }

}