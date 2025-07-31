using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Octopus.Tentacle.Core.Diagnostics
{
    static class TypeExtensionMethods
    {
        public static bool IsClosedGenericOfType(this Type typeToCheck, Type openGenericType)
        {
            if (!openGenericType.IsGenericType)
                return false;
            var closedGenericOfExceptionTypes = typeToCheck.ClosedGenericOfExceptionTypes(openGenericType);
            return closedGenericOfExceptionTypes.Length > 0;
        }

        public static Type[] ClosedGenericOfExceptionTypes(this Type typeToCheck, Type openGenericType)
        {
            return typeToCheck.GetCompleteHierarchy()
                .Where(t => t.IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == openGenericType)
                .Select(t => t.GetGenericArguments().First())
                .ToArray();
        }

        static IEnumerable<Type> GetCompleteHierarchy(this Type type)
        {
            yield return type;

            var interfaceTypes = type.GetInterfaces()
                .SelectMany(i => i.GetCompleteHierarchy())
                .Distinct()
                .ToArray();
            foreach (var t in interfaceTypes) yield return t;

            var baseTypes = type.BaseType?.GetCompleteHierarchy() ?? new Type[0];
            foreach (var t in baseTypes) yield return t;
        }
    }
}