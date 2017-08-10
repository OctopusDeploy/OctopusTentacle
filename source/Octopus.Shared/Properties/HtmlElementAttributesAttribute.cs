using System;

namespace Octopus.Shared.Properties
{
    [AttributeUsage(
        AttributeTargets.Parameter | AttributeTargets.Property |
            AttributeTargets.Field)]
    public sealed class HtmlElementAttributesAttribute : Attribute
    {
        public HtmlElementAttributesAttribute()
        {
        }

        public HtmlElementAttributesAttribute([NotNull] string name)
        {
            Name = name;
        }

        [NotNull]
        public string Name { get; private set; }
    }
}