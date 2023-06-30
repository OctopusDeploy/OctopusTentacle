using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class RpcCall
    {
        public string Service { get; }
        public string Name { get; }

        public RpcCall(string service, string name)
        {
            Service = service;
            Name = name;
        }

        public override string ToString() => $"{Service}.{Name}";

        public static RpcCall Create<T>(string name) => new(typeof(T).Name, name);
    }
}