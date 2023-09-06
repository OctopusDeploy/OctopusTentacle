﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    internal class RandomTemporaryFileBuilder
    {
        private int sizeInMb = 2;

        public RandomTemporaryFileBuilder WithSizeInMb(int sizeInMb)
        {
            this.sizeInMb = sizeInMb;

            return this;
        }

        public RandomTemporaryFile Build()
        {
            var tempFile = Path.GetTempFileName();
            var data = new byte[sizeInMb * 1024 * 1024];
            var rng = new Random();
            rng.NextBytes(data);
            File.WriteAllBytes(tempFile, data);
            return new RandomTemporaryFile(new FileInfo(tempFile));
        }
    }

    internal class RandomTemporaryFile : IDisposable
    {
        public FileInfo File { get; }

        public RandomTemporaryFile(FileInfo file)
        {
            File = file;
        }

        public void Dispose()
        {
            if (File.Exists)
            {
                try
                {
                    _ = Task.Run(() => File.Delete());
                }
                catch
                {
                    // We sometimes have tests failing due to this file being locked during teardown.
                    // As this is class purely for testing, and we regularly recycle our build agents,
                    // we are happy with a 'best effort' approach here.
                }
            }
        }
    }
}
