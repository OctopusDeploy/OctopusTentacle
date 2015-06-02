using System;
using System.IO;

namespace Octopus.Shared.PackageConversion.Streams
{
    public static class StreamExtensions
    {
        public static void CopyTo(this Stream source, Stream destination, long length, int bufferSize = 1024*4)
        {
            var buffer = new byte[bufferSize]; // 4K is optimum
            while (length > 0)
            {
                var bytesToRead = (int)(length > bufferSize ? bufferSize : length);
                length = length - bytesToRead;

                var numRead = source.Read(buffer, 0, bytesToRead);
                if (numRead <= 0)
                {
                    break;
                }
                destination.Write(buffer, 0, numRead);
            }
        }
    }
}