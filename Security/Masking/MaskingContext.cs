using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Octopus.Shared.Security.Masking
{
    public static class MaskingContext
    {
        // Using a CWT for this might be worthwhile if you consider pathological scenarios.
        static readonly ConcurrentDictionary<SensitiveDataMask, bool> Masks = new ConcurrentDictionary<SensitiveDataMask, bool>();
        static readonly SensitiveDataMask permanent = new SensitiveDataMask();

        static MaskingContext()
        {
            Add(permanent);
        }

        public static SensitiveDataMask Permanent { get { return permanent; } }

        public static IDisposable Add(SensitiveDataMask mask)
        {
            if (mask == null) throw new ArgumentNullException("mask");
            Masks.TryAdd(mask, true);
            return new MaskRemover(Masks, mask);
        }

        public static string ApplyTo(string raw)
        {
            return Masks.Keys.Aggregate(raw, (s, m) => m.ApplyTo(s));
        }

        public static Exception ApplyTo(Exception exception)
        {
            return Masks.Keys.Aggregate(exception, (s, m) => m.ApplyTo(s));
        }

        sealed class MaskRemover : IDisposable
        {
            readonly ConcurrentDictionary<SensitiveDataMask, bool> masks;
            readonly SensitiveDataMask mask;

            public MaskRemover(ConcurrentDictionary<SensitiveDataMask, bool> masks, SensitiveDataMask mask)
            {
                this.masks = masks;
                this.mask = mask;
            }

            public void Dispose()
            {
                bool unused;
                masks.TryRemove(mask, out unused);
            }
        }
    }
}
