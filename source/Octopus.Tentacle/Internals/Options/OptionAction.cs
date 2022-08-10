using System;

namespace Octopus.Tentacle.Internals.Options
{
    public delegate void OptionAction<TKey, TValue>(TKey key, TValue value);
}