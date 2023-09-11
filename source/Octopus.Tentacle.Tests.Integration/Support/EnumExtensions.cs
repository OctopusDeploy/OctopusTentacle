using System;
using System.ComponentModel;
using System.Linq;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum enumValue)
        {
            var attr = enumValue
                .GetType()
                .GetField(enumValue.ToString())!
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .Cast<DescriptionAttribute>()
                .FirstOrDefault();

            if (attr == null)
                throw new Exception($"Enum value '{enumValue}' doesn't have Description attribute");

            return attr.Description;
        }
    }
}
