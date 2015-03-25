using System;

namespace Octopus.Shared.Packages
{
    public class StagedPackage
    {
        readonly PackageMetadata package;
        readonly string fullPathOnRemoteMachine;
        readonly string hash;
        readonly long length;

        public StagedPackage(PackageMetadata package, string fullPathOnRemoteMachine, string hash, long length)
        {
            this.package = package;
            this.fullPathOnRemoteMachine = fullPathOnRemoteMachine;
            this.hash = hash;
            this.length = length;
        }

        public PackageMetadata Package
        {
            get { return package; }
        }

        public string FullPathOnRemoteMachine
        {
            get { return fullPathOnRemoteMachine; }
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