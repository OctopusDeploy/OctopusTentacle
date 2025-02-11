namespace Octopus.Tentacle.Client.Scripts
{
    public record ScriptServiceVersion(string Value)
    {
        public static ScriptServiceVersion ScriptServiceVersion1 = new(nameof(ScriptServiceVersion1));
        public static ScriptServiceVersion ScriptServiceVersion2 = new(nameof(ScriptServiceVersion2));
        public static ScriptServiceVersion KubernetesScriptServiceVersion1 = new(nameof(KubernetesScriptServiceVersion1));

        public override string ToString() => Value;
    }
}
