using System;

namespace Octopus.Shared.Packages
{
    public class StagedPackage
    {
        readonly string hash;
        readonly long length;

        public StagedPackage(string hash, long length)
        {
            this.hash = hash;
            this.length = length;
        }

        public long Length
        {
            get { return length; }
        }

        public string Hash
        {
            get { return hash; }
        }
    }
}