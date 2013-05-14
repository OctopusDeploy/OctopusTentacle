using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using System.Runtime.Caching;

namespace Octopus.Shared.Caching
{
    public interface ICache<TItem> where TItem : class
    {
        void Set(string key, TItem item, DateTimeOffset absoluteExpiry);
        TItem Get(string key);
        void Clear();
    }

    public class OctopusCache<TItem> : ICache<TItem> where TItem : class
    {
        readonly MemoryCache implementation;
        readonly object syncRoot = new object();

        public OctopusCache(string name)
        {
            implementation = new MemoryCache(name);
        }

        public TItem Get(string key)
        {
            return implementation.Get(key) as TItem;
        }

        public void Set(string key, TItem item, DateTimeOffset absoluteExpiry)
        {
            implementation.Set(key, item, absoluteExpiry);
        }

        public void Clear()
        {
            lock (syncRoot)
            {
                var enumerable = ((IEnumerable<KeyValuePair<string, object>>) implementation);
                var keysToRemove = enumerable.Select(kvp => kvp.Key).ToList();

                foreach (var key in keysToRemove)
                {
                    implementation.Remove(key);
                }
            }
        }
    }
}