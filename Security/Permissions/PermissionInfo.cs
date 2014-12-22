using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.Permissions
{
    public static class PermissionInfo
    {
        public static IList<Permission> GetAllPermissions()
        {
            return typeof (Permission)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.GetCustomAttributes(typeof (DescriptionAttribute), false).Length > 0)
                .Select(f => (Permission) f.GetValue(null))
                .ToList();
        } 

        public static IList<string> GetSupportedRestrictions(Permission permission)
        {
            // ReSharper disable once PossibleNullReferenceException
            var restrictable = typeof(Permission)
                .GetField(permission.ToString(), BindingFlags.Static | BindingFlags.Public)
                .GetCustomAttributes(typeof(SupportsRestrictionAttribute), false)
                .Cast<SupportsRestrictionAttribute>()
                .SingleOrDefault();

            if (restrictable == null)
                return new List<string>();

            return restrictable.Scopes;
        }

        public static string GetDescription(Permission permission)
        {
            // ReSharper disable once PossibleNullReferenceException
            var description = typeof(Permission)
                .GetField(permission.ToString(), BindingFlags.Static | BindingFlags.Public)
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .Cast<DescriptionAttribute>()
                .SingleOrDefault();

            if (description == null)
                return permission.ToString();

            return description.Description;
        }
    }
}