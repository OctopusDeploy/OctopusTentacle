using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{

    public class SocketPump
    {
        public delegate bool IsPumpPaused();

        Stopwatch stopwatch = Stopwatch.StartNew();
        MemoryStream buffer = new MemoryStream();
        private readonly TcpPump tcpPump;
        IsPumpPaused isPumpPaused;
        readonly TimeSpan sendDelay;

        IDataTransferObserver dataTransferObserver;

        public SocketPump(TcpPump tcpPump, IsPumpPaused isPumpPaused, TimeSpan sendDelay, IDataTransferObserver dataTransferObserver)
        {
            this.tcpPump = tcpPump;
            this.isPumpPaused = isPumpPaused;
            this.sendDelay = sendDelay;
            this.dataTransferObserver = dataTransferObserver;
        }

        public async Task<SocketStatus> PumpBytes(Socket readFrom, Socket writeTo, CancellationToken cancellationToken)
        {
            await PausePump(cancellationToken);

            // Only read if we have nothing to send or if data exists 
            if (readFrom.Available > 0 || buffer.Length == 0)
            {
                var receivedByteCount = await ReadFromSocket(readFrom, buffer, cancellationToken);
                if (receivedByteCount == 0) return SocketStatus.SOCKET_CLOSED;
                stopwatch = Stopwatch.StartNew();
            }


            await PausePump(cancellationToken);

            if ((readFrom.Available == 0 && stopwatch.Elapsed >= sendDelay) || buffer.Length > 100 * 1024 * 1024)
            {
                // Send the data
                dataTransferObserver.WritingData(tcpPump, buffer);
                await WriteToSocket(writeTo, buffer.GetBuffer(), (int)buffer.Length, cancellationToken);
                buffer.SetLength(0);
            }
            else
            {
                await Task.Delay(1, cancellationToken);
            }

            return SocketStatus.SOCKET_OPEN;
        }

        async Task PausePump(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (isPumpPaused())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        static async Task WriteToSocket(Socket writeTo, byte[] inputBuffer, int totalBytesToSend, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (totalBytesToSend - offset > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArraySegment<byte> outputBuffer = new ArraySegment<byte>(inputBuffer, offset, totalBytesToSend - offset);
#if DOES_NOT_SUPPORT_CANCELLATION_ON_SOCKETS
                offset += await writeTo.SendAsync(outputBuffer, SocketFlags.None).ConfigureAwait(false);
#else
                offset += await writeTo.SendAsync(outputBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
#endif
            }

        }

        static async Task<int> ReadFromSocket(Socket readFrom, MemoryStream memoryStream, CancellationToken cancellationToken)
        {
            var inputBuffer = new byte[readFrom.ReceiveBufferSize];
            ArraySegment<byte> inputBufferArraySegment = new ArraySegment<byte>(inputBuffer);
#if DOES_NOT_SUPPORT_CANCELLATION_ON_SOCKETS
            var receivedByteCount = await readFrom.ReceiveAsync(inputBufferArraySegment, SocketFlags.None).ConfigureAwait(false);
#else
            var receivedByteCount = await readFrom.ReceiveAsync(inputBufferArraySegment, SocketFlags.None, cancellationToken).ConfigureAwait(false);
#endif

            memoryStream.Write(inputBuffer, 0, receivedByteCount);
            return receivedByteCount;
        }


        public enum SocketStatus
        {
            SOCKET_CLOSED,
            SOCKET_OPEN
        }
    }
}