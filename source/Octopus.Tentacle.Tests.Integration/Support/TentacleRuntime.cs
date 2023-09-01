using System;
using System.Linq;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public enum TentacleRuntime
    {
        Default,
        
        [StringValue("net6.0")]
        DotNet6,
        
        [StringValue("net48")]
        Framework48
    }

    
    public class StringValueAttribute : Attribute
    {
        public string Value { get; }

        public StringValueAttribute(string value)
        {
            Value = value;
        }
    }

    public static class EnumExtensions
    {
        public static string GetStringValue(this Enum enumValue)
        {
            var attr = enumValue
                .GetType()
                .GetField(enumValue.ToString())!
                .GetCustomAttributes(typeof(StringValueAttribute), false)
                .Cast<StringValueAttribute>()
                .FirstOrDefault();

            if (attr == null)
                throw new Exception($"Enum value '{enumValue}' doesn't have StringValueAttribute");

            return attr.Value;
        }
    }
}
