using System;

namespace Octopus.Platform.Licensing
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AssemblyBuildDateAttribute : Attribute
    {
        readonly string buildDateTimeUtc;

        public AssemblyBuildDateAttribute(string buildDateTimeUtc)
        {
            this.buildDateTimeUtc = buildDateTimeUtc;
        }

        public string BuildDateTimeUtc
        {
            get { return buildDateTimeUtc; }
        }
    }
}
