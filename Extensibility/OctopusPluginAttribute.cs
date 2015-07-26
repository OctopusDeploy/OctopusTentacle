using System;
using System.ComponentModel.Composition;

namespace Octopus.Shared.Extensibility
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class OctopusPluginAttribute : ExportAttribute, IOctopusExtensionMetadata
    {
        public OctopusPluginAttribute(string friendlyName)
            : base(typeof(IOctopusExtension))
        {
            FriendlyName = friendlyName;
        }

        public string FriendlyName { get; set; }
    }
}