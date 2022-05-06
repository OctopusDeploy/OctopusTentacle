using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Octopus.Shared.Util
{
    public static class TypeExtensions
    {
        public static string DisplayName(this MemberInfo typeOrMember)
        {
            if (typeOrMember == null) throw new ArgumentNullException("typeOrMember");
            var da = typeOrMember.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().SingleOrDefault();
            if (da != null)
                return da.Description;
            return typeOrMember.Name;
        }

        public static bool IsDecoratedWith<TAttribute>(this MemberInfo typeOrMember)
        {
            if (typeOrMember == null) throw new ArgumentNullException("typeOrMember");
            return typeOrMember.GetCustomAttributes(typeof(TAttribute), false).Any();
        }
    }
}