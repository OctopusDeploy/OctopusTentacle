using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Configuration
{
    public interface IWritableKeyValueStore : IKeyValueStore
    {
        bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None);

        bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None);

        bool Remove(string name);

        bool Save();

        Task<bool> SaveAsync(CancellationToken cancellationToken = default);

        Task<bool> SetAsync(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None, CancellationToken cancellationToken = default);

        Task<bool> SetAsync<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None, CancellationToken cancellationToken = default);

        Task<bool> RemoveAsync(string name, CancellationToken cancellationToken = default);
    }
}