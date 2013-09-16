using System;
using System.IO;

namespace Octopus.Shared.Bcl.IO
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                wrappedStream.Dispose();
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
        {
            return wrappedStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            wrappedStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return wrappedStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            wrappedStream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return wrappedStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return wrappedStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return wrappedStream.CanWrite; }
        }

        public override long Length
        {
            get { return wrappedStream.Length; }
        }

        public override long Position
        {
            get { return wrappedStream.Position; }
            set { wrappedStream.Position = value; }
        }
    }
}
