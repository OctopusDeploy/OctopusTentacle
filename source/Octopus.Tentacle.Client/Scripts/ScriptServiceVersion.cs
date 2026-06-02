namespace Octopus.Tentacle.Client.Scripts
{
    public record ScriptServiceVersion(string Value)
    {
        public static ScriptServiceVersion ScriptServiceVersion1 = new(nameof(ScriptServiceVersion1));
        public static ScriptServiceVersion ScriptServiceVersion2 = new(nameof(ScriptServiceVersion2));
        public static ScriptServiceVersion KubernetesScriptServiceVersion1 = new(nameof(KubernetesScriptServiceVersion1));

        // Only ScriptServiceV2 has the AbandonScript verb, so the orchestrator checks this before it
        // escalates a stuck cancel to abandon and never calls abandon where it can't work. This assumes
        // a V2 tentacle advertises AbandonScript (true from this build forward); the server doesn't enable
        // abandon against older tentacles, so we don't re-check the capability per call here.
        public bool SupportsAbandon => Value == nameof(ScriptServiceVersion2);

        public override string ToString() => Value;
    }
}
