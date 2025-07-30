using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.SocksProxy
{

    /// <summary>
    /// A Stream implementation that wraps a ClientWebSocket to provide standard Stream interface.
    /// </summary>
    public class ClientWebSocketStream : Stream
    {
        private readonly WebSocket _webSocket;
        private readonly CancellationToken _cancellationToken;
        private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        private bool _disposed = false;

        public ClientWebSocketStream(WebSocket webSocket)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _cancellationToken = CancellationToken.None;

        }

        public ClientWebSocketStream(ClientWebSocket webSocket, CancellationToken cancellationToken)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _cancellationToken = cancellationToken;
        }

        public override bool CanRead => _webSocket.State == WebSocketState.Open;
        public override bool CanSeek => false;
        public override bool CanWrite => _webSocket.State == WebSocketState.Open;
        public override long Length => throw new NotSupportedException("WebSocket streams do not support seeking");

        public override long Position
        {
            get => throw new NotSupportedException("WebSocket streams do not support seeking");
            set => throw new NotSupportedException("WebSocket streams do not support seeking");
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var effectiveCancellationToken = CombineCancellationTokens(cancellationToken);
            await _readSemaphore.WaitAsync(effectiveCancellationToken);

            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), effectiveCancellationToken);

                return result.Count;
            }
            finally
            {
                _readSemaphore.Release();
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var effectiveCancellationToken = CombineCancellationTokens(cancellationToken);
            await _writeSemaphore.WaitAsync(effectiveCancellationToken);

            try
            {
                #if NET8_0_OR_GREATER
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, WebSocketMessageFlags.None, effectiveCancellationToken);
                #else
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, false, effectiveCancellationToken);
                #endif
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override void Flush()
        {
            // WebSockets send immediately, so flush is a no-op
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("WebSocket streams do not support seeking");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("WebSocket streams do not support seeking");
        }

        private CancellationToken CombineCancellationTokens(CancellationToken externalToken)
        {
            if (externalToken == CancellationToken.None)
                return _cancellationToken;

            if (_cancellationToken == CancellationToken.None)
                return externalToken;

            var source = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, externalToken);
            return source.Token;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Close the WebSocket if it's still open
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        Log.Information("Closing WebSocket connection gracefully...");
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stream disposed", CancellationToken.None)
                            .GetAwaiter().GetResult();
                    }

                    _readSemaphore.Dispose();
                    _writeSemaphore.Dispose();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
