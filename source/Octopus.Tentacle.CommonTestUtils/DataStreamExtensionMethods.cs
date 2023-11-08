using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut;

namespace Octopus.Tentacle.CommonTestUtils
{
    public static class DataStreamExtensionMethods
    {
        public static async Task<byte[]> ToBytes(this DataStream dataStream, CancellationToken cancellationToken)
        {
            byte[]? bytes = null;

            await dataStream.Receiver()
                .ReadAsync(
                    async (stream, ct) =>
                    {
                        if (stream is MemoryStream ms)
                        {
                            bytes = ms.ToArray();
                        }
                        else
                        {
                            using var memoryStream = new MemoryStream();

                            await stream.CopyToAsync(memoryStream, 81920, ct);
                            bytes = memoryStream.ToArray();
                        }
                    }, cancellationToken);

            return bytes!;
        }
        
        public static async Task<string> GetUtf8String(this DataStream dataStream, CancellationToken cancellationToken)
        {
            var bytes = await dataStream.ToBytes(cancellationToken);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
