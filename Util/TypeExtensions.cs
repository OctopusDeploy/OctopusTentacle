using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Octopus.Platform.Util
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
    }
}