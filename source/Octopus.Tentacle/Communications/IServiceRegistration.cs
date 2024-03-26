using System;

namespace Octopus.Tentacle.Communications
{
    public interface IServiceRegistration
    {
        T GetService<T>();

        bool TryGetService<T>(out T service);
    }
}