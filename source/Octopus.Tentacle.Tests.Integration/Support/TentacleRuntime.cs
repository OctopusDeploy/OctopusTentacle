using System;
using System.Linq;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public enum TentacleRuntime
    {
        [StringValue("Default")]
        Default,
        
        [StringValue(RuntimeDetection.DotNet6)]
        DotNet6,
        
        [StringValue(RuntimeDetection.Framework48)]
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
