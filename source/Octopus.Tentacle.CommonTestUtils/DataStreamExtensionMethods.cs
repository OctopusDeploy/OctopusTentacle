using System.IO;
using System.Text;
using Halibut;

namespace Octopus.Tentacle.CommonTestUtils
{
    public static class DataStreamExtensionMethods
    {
        public static byte[] ToBytes(this DataStream dataStream)
        {
            byte[]? bytes = null;
#pragma warning disable CS0612
            dataStream.Receiver()
                .Read(
                    stream =>
                    {
                        if (stream is MemoryStream ms)
                        {
                            bytes = ms.ToArray();
                        }
                        else
                        {
                            using var memoryStream = new MemoryStream();

                            stream.CopyTo(memoryStream);
                            bytes = memoryStream.ToArray();
                        }
                    });
#pragma warning restore CS0612

            return bytes!;
        }

        public static string GetString(this DataStream dataStream, Encoding encoding) => encoding.GetString(dataStream.ToBytes());
        
        public static string GetUtf8String(this DataStream dataStream) => dataStream.GetString(Encoding.UTF8);
    }
}
