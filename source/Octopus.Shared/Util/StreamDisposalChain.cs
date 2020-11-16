using System;
using System.IO;

namespace Octopus.Shared.Util
{
    public class StreamDisposalChain : Stream
    {
        readonly Stream wrappedStream;
        readonly IDisposable[] dependenciesInDisposalOrder;

        public StreamDisposalChain(Stream wrappedStream, params IDisposable[] dependenciesInDisposalOrder)
        {
            if (wrappedStream == null) throw new ArgumentNullException("wrappedStream");
            if (dependenciesInDisposalOrder == null) throw new ArgumentNullException("dependenciesInDisposalOrder");
            this.wrappedStream = wrappedStream;
            this.dependenciesInDisposalOrder = dependenciesInDisposalOrder;
        }

        public override bool CanRead => wrappedStream.CanRead;

        public override bool CanSeek => wrappedStream.CanSeek;

        public override bool CanWrite => wrappedStream.CanWrite;

        public override long Length => wrappedStream.Length;

        public override long Position
        {
            get => wrappedStream.Position;
            set => wrappedStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    wrappedStream.Dispose();
                }
                catch (Exception)
                {
                }

                foreach (var disposable in dependenciesInDisposalOrder)
                    disposable.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void Flush()
        {
            wrappedStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
            => wrappedStream.Seek(offset, origin);

        public override void SetLength(long value)
        {
            wrappedStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
            => wrappedStream.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count)
        {
            wrappedStream.Write(buffer, offset, count);
        }
    }
}