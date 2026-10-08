using System;

namespace Octopus.Tentacle.Configuration
{
    public abstract class HierarchicalDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        protected HierarchicalDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData? Get<TData>(string name, TData? defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None) where TData : default
            => throw new NotImplementedException("This ");

        public override bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (value == null || value is string s && string.IsNullOrWhiteSpace(s))
            {
                Write(name, null);
                if (AutoSaveOnSet)
                    return Save();
                return true;
            }

            var valueAsObject = (object)value;

            // This store only ever backs `show-configuration` output, which is never read back by Tentacle, so there is
            // no key it could sensibly be encrypted with. Refuse rather than write a secret somewhere nothing can decrypt.
            if (protectionLevel == ProtectionLevel.MachineKey)
                throw new NotSupportedException($"{GetType().Name} cannot store {nameof(ProtectionLevel.MachineKey)} values; it is only used to display configuration.");

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                return Save();

            return true;
        }
    }
}