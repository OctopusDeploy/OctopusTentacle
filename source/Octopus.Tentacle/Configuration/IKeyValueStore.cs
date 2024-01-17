using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Configuration
{
    public interface IKeyValueStore
    {
        string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None);

        [return: NotNullIfNotNull("defaultValue")]
        TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None);
    }
}