namespace Octopus.Tentacle.Configuration
{
    public interface IWritableKeyValueStore : IKeyValueStore
    {
        bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None);

        bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None);

        bool Remove(string name);

        bool Save();
    }
}