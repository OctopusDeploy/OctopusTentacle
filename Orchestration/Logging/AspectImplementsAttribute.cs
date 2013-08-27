using System;

namespace Octopus.Shared.Orchestration.Logging
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AspectImplementsAttribute : Attribute
    {
        readonly Type[] implementedTypes;

        public AspectImplementsAttribute(params Type[] implementedTypes)
        {
            this.implementedTypes = implementedTypes;
        }

        public Type[] ImplementedTypes
        {
            get { return implementedTypes; }
        }
    }
}