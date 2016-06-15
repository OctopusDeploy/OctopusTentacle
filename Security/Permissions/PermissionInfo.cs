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
        static readonly Dictionary<Permission, PermissionMetaData> Permissions = typeof(Permission)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .ToDictionary(t => (Permission)t.GetValue(null), ExtractMetaData);

        public static IList<Permission> GetAllPermissions()
        {
            return Permissions.Keys.Where(p => p!= Permission.None).ToList();
        }

        public static IList<string> GetSupportedRestrictions(Permission permission)
        {
            return Permissions[permission].Scopes;
        }

        public static string GetDescription(Permission permission)
        {
            return Permissions[permission].Description;
        }
        static PermissionMetaData ExtractMetaData(FieldInfo t)
        {
            var name = t.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .Cast<DescriptionAttribute>()
                .SingleOrDefault()?.Description ?? t.Name;

            var scopes = t.GetCustomAttributes(typeof(SupportsRestrictionAttribute), false)
                .Cast<SupportsRestrictionAttribute>()
                .SingleOrDefault()?.Scopes ?? new List<string>();

            return new PermissionMetaData(name, scopes);
        }

        class PermissionMetaData
        {
            public string Description { get; }
            public IList<string> Scopes { get; }

            public PermissionMetaData(string description, IList<string> scopes)
            {
                Description = description;
                Scopes = scopes;
            }
        }
    }
}