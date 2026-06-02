namespace Octopus.Tentacle.Client.Scripts
{
    public record ScriptServiceVersion(string Value)
    {
        public static ScriptServiceVersion ScriptServiceVersion1 = new(nameof(ScriptServiceVersion1));
        public static ScriptServiceVersion ScriptServiceVersion2 = new(nameof(ScriptServiceVersion2));
        public static ScriptServiceVersion KubernetesScriptServiceVersion1 = new(nameof(KubernetesScriptServiceVersion1));

        // Only ScriptServiceV2 has the AbandonScript verb. The orchestrator checks this before it
        // escalates a stuck cancel to abandon, so we never call abandon where it can't work.
        public bool SupportsAbandon => Value == nameof(ScriptServiceVersion2);

        public override string ToString() => Value;
    }
}
