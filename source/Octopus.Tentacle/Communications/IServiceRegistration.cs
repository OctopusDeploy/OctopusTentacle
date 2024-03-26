using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Communications
{
    public interface IServiceRegistration
    {
        T GetService<T>();

        bool TryGetService<T>([NotNullWhen(true)]out T? service) where T : class;
    }
}