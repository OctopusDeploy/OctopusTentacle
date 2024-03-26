using System;

namespace Octopus.Tentacle.Communications
{
    public interface IServiceRegistration
    {
        T GetService<T>();

        bool TryGetService<T>(bool b, out T? service) where T : class;
    }
}