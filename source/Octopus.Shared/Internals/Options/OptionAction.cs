using System;

namespace Octopus.Shared.Internals.Options
{
    public delegate void OptionAction<TKey, TValue>(TKey key, TValue value);
}