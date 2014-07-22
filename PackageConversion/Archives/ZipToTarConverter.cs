using System;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;

namespace Octopus.Shared.PackageConversion.Archives
{
    public static class ZipToTarConverter
    {
        public static void Convert(Stream inputStream, Stream outputStream, Action<float, long, long> progress = null)
        {
            var zipInputStream = new ZipFile(inputStream);
            var tarOutputStream = new TarOutputStream(outputStream);

            // We use the entry length to mark progress because inputStream.Length includes header sizes.
            var entries = zipInputStream.Cast<ZipEntry>().ToArray();
            var totalZipEntryLength = entries.Aggregate(0L, (l, entry) => l += entry.CompressedSize);
            var remainder = totalZipEntryLength;

            foreach (ZipEntry zipEntry in entries)
            {
                var entryFileName = zipEntry.Name;
                var tarEntry = TarEntry.CreateTarEntry(entryFileName);
                tarEntry.Size = zipEntry.Size;
                tarOutputStream.PutNextEntry(tarEntry);

                if (zipEntry.IsFile)
                {
                    // Extract the file from the zip
                    using (var zipStream = zipInputStream.GetInputStream(zipEntry))
                    {
                        // Tar it up
                        zipStream.CopyTo(tarOutputStream);
                        
                        remainder -= zipEntry.CompressedSize;
                        if (progress != null)
                        {
                            progress(remainder / (float)totalZipEntryLength, remainder, totalZipEntryLength);
                        }
                    }
                }

                tarOutputStream.CloseEntry();
            }

            tarOutputStream.Finish();
            tarOutputStream.Flush();

            tarOutputStream.IsStreamOwner = false;
            tarOutputStream.Close();
        }

    }
}
