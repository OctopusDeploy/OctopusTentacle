using System;

namespace Octopus.Tentacle.Communications
{
    public interface IHalibutInitializer
    {
        void Start();
        void Stop();
    }
}