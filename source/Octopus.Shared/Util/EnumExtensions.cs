using System;
using System.ComponentModel;

namespace Octopus.Shared.Util
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum enumVal)
            => enumVal.GetAttributeOfType<DescriptionAttribute>()?.Description ?? enumVal.ToString();

        public static T? GetAttributeOfType<T>(this Enum enumVal) where T : Attribute
        {
            var type = enumVal.GetType();
            var memInfo = type.GetMember(enumVal.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
            return attributes.Length > 0 ? (T)attributes[0] : null;
        }
    }
}