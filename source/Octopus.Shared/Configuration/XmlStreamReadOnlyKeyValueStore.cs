using System;
using System.IO;

namespace Octopus.Shared.Configuration
{
    public class XmlStreamReadOnlyKeyValueStore : XmlKeyValueStore
    {
        readonly Stream s;

        public XmlStreamReadOnlyKeyValueStore(Stream s) : base(false)
        {
            this.s = s;
        }

        protected override bool ExistsForReading()
            => true;

        protected override Stream OpenForReading()
        {
            s.Seek(0, SeekOrigin.Begin);
            return s;
        }

        protected override Stream OpenForWriting()
            => throw new NotSupportedException("Cannot write to these settings.");
    }
}