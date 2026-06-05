namespace Octopus.Tentacle.Client.Scripts
{
    public record ScriptServiceVersion(string Value)
    {
        public static ScriptServiceVersion ScriptServiceVersion1 = new(nameof(ScriptServiceVersion1));
        public static ScriptServiceVersion ScriptServiceVersion2 = new(nameof(ScriptServiceVersion2));
        public static ScriptServiceVersion ScriptServiceVersion2WithAbandon = new(nameof(ScriptServiceVersion2WithAbandon));
        public static ScriptServiceVersion KubernetesScriptServiceVersion1 = new(nameof(KubernetesScriptServiceVersion1));

        // Only a V2 Tentacle that advertised the AbandonScript capability supports abandon. Old V2
        // Tentacles predate the verb, so they select ScriptServiceVersion2, not ...WithAbandon.
        public bool SupportsAbandon => Value == nameof(ScriptServiceVersion2WithAbandon);

        public override string ToString() => Value;
    }
}
