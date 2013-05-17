using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Octopus.Shared.Util
{
    /// <summary>
    /// The one and only <see cref="AmazingConverter"/>. Can convert from absolutely anything to absolutely 
    /// anything.
    /// </summary>
    public static class AmazingConverter
    {
        /// <summary>
        /// If it can be converted, the <see cref="AmazingConverter"/> will figure out how. Given a source
        /// object, tries its best to convert it to the target type.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="targetType">The type to convert the source object to.</param>
        /// <returns></returns>
        public static object Convert(object source, Type targetType)
        {
            if (source == null)
            {
                // Returns the default(T) of the type
                return targetType.IsValueType
                    ? Activator.CreateInstance(targetType)
                    : null;
            }

            var sourceType = source.GetType();

            // Try casting
            if (targetType.IsAssignableFrom(sourceType))
                return source;

            // Try type descriptors
            var targetConverter = TypeDescriptor.GetConverter(targetType);
            if (targetConverter.CanConvertFrom(sourceType))
            {
                return targetConverter.ConvertFrom(source);
            }

            var sourceConverter = TypeDescriptor.GetConverter(sourceType);
            if (sourceConverter.CanConvertTo(targetType))
            {
                return sourceConverter.ConvertTo(source, targetType);
            }

            // Find an implicit assignment converter
            var implicitAssignment = targetType.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static)
                .Where(x => x.Name == "op_Implicit")
                .Where(x => targetType.IsAssignableFrom(x.ReturnType))
                .FirstOrDefault();

            if (implicitAssignment != null)
            {
                return implicitAssignment.Invoke(null, new[] { source });
            }

            // Hope and pray
            return source;
        }
    }
}
