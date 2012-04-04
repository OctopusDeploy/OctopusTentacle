using System;

namespace Octopus.Shared.Contracts
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class SuffixAttribute : Attribute
    {
        public SuffixAttribute(string suffix)
        {
            Suffix = suffix;
        }

        public string Suffix { get; set; }
    }
}