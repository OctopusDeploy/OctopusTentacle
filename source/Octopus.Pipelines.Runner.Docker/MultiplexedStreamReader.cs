using System.Buffers.Binary;
using Octopus.Pipelines.Runner.Core;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Reads Docker's multiplexed stdout/stderr stream (a container without a TTY). Each frame is an 8-byte header,
/// [stream type, 0, 0, 0, big-endian uint32 payload length], then the payload. Frames can split across reads.
/// </summary>
static class MultiplexedStreamReader
{
    const byte Stdout = 1;
    const byte Stderr = 2;
    const int HeaderLength = 8;

    /// <summary>Writes every stdout and stderr payload to <paramref name="output"/>, in order, until the stream ends.</summary>
    public static async Task CopyAsync(Stream source, IJobOutput output, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
            if (read < HeaderLength)
            {
                // The stream ends when the container does; a partial header means it was cut short.
                return;
            }

            var streamType = header[0];
            var remaining = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
            var keep = streamType is Stdout or Stderr;

            // Pass a large frame on as it arrives rather than buffering all of it.
            while (remaining > 0)
            {
                var chunk = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(remaining, (uint)buffer.Length)), cancellationToken);
                if (chunk == 0)
                {
                    return;
                }

                if (keep)
                {
                    output.Write(buffer.AsSpan(0, chunk));
                }

                remaining -= (uint)chunk;
            }
        }
    }
}
