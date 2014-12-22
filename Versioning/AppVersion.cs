using System;
using System.Reflection;
using Octopus.Shared.Util;

namespace Octopus.Shared.Versioning
{
    public class AppVersion
    {
        public AppVersion(int major, int minor, int path, int build)
        {
            Major = major;
            Minor = minor;
            Path = build;
            Build = build;
        }

        public AppVersion(Assembly assembly)
            : this(assembly.GetFileVersion())
        {
        }

        public AppVersion(string version)
            : this(new Version(version))
        {
        }


        public AppVersion(Version version)
        {
            Major = version.Major;
            Minor = version.Minor;
            Path = version.Build;
            Build = version.Revision;
        }

        public int Major { get; set; }
        public int Minor { get; set; }
        public int Path { get; set; }
        public int Build { get; set; }

        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}.{3}", Major, Minor, Path, Build);
        }
    }
}
