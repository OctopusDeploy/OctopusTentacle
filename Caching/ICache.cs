using System;

namespace Octopus.Shared.Caching
{
    public interface ICache<TItem> where TItem : class
    {
        void Set(string key, TItem item, DateTimeOffset absoluteExpiry);
        TItem Get(string key);
        void Delete(string key);
        void Clear();
    }
}