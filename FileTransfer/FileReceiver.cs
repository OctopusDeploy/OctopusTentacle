﻿using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.FileTransfer
{
    public class FileReceiver : PersistentActor<FileReceiveData>,
                                ICreatedBy<BeginFileTransferCommand>,
                                IReceiveAsync<SendNextChunkReply>
    {
        readonly IFileStorageConfiguration fileStorageConfiguration;
        readonly IOctopusFileSystem fileSystem;
        readonly ISupervised supervised;

        public FileReceiver(
            IFileStorageConfiguration fileStorageConfiguration,
            IOctopusFileSystem fileSystem)
        {
            this.fileStorageConfiguration = fileStorageConfiguration;
            this.fileSystem = fileSystem;
            supervised = RegisterAspect(new Supervised(config=>
                config.OnProcessTimeout(() => Log.Octopus().ErrorFormat("Transfer of {0} did not complete before the process timeout", Data.LocalPath))));
        }

        public void Receive(BeginFileTransferCommand message)
        {
            var path = Path.Combine(
                fileStorageConfiguration.FileStorageDirectory,
                message.Filename + "-" + Guid.NewGuid());

            Data = new FileReceiveData
            {
                LocalPath = path,
                Hash = message.Hash
            };
            
            Log.Octopus().InfoFormat("Beginning transfer of {0}", Data.LocalPath);
            supervised.Notify(new SendNextChunkRequest());
        }

        public async Task ReceiveAsync(SendNextChunkReply message)
        {
            using (var file = fileSystem.OpenFile(Data.LocalPath, FileMode.OpenOrCreate))
            {
                file.Seek(0, SeekOrigin.End);
                file.Write(message.Data, 0, message.Data.Length);
            }

            if (message.IsLastChunk)
            {
                using (var file = fileSystem.OpenFile(Data.LocalPath, FileAccess.Read))
                {
                    var hash = HashCalculator.Hash(file);
                    if (hash != Data.Hash)
                    {
                        file.Dispose();
                        fileSystem.DeleteFile(Data.LocalPath, DeletionOptions.TryThreeTimesIgnoreFailure);
                        supervised.Fail("The file corrupted during transfer");
                        return;
                    }
                }

                supervised.Succeed(new FileTransferCompleteEvent(Data.LocalPath));
                return;
            }

            supervised.Notify(new SendNextChunkRequest());
        }
    }
}

