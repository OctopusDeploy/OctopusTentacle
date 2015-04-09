using System;

namespace Octopus.Shared.Variables
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DeprecatedAliasAttribute : Attribute
    {
        public string[] Aliases { get; private set; }

        public DeprecatedAliasAttribute(params string[] aliases)
        {
            Aliases = aliases;
        }
    }
}