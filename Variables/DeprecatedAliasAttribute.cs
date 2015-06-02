using System;

namespace Octopus.Shared.Variables
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DeprecatedAliasAttribute : Attribute
    {
        public DeprecatedAliasAttribute(params string[] aliases)
        {
            Aliases = aliases;
        }

        public string[] Aliases { get; private set; }
    }
}