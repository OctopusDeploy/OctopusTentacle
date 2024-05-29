using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Util;

public static class ExecuteKubernetesScriptCommandBuilderExtensionMethods
{
    public static ExecuteKubernetesScriptCommandBuilder WithScriptBody(this ExecuteKubernetesScriptCommandBuilder builder, Action<ScriptBuilder> scriptBuilderFunc)
    {
        var scriptBuilder = new ScriptBuilder();
        scriptBuilderFunc(scriptBuilder);

        return builder.WithScriptBody(scriptBuilder);
    }

    public static ExecuteKubernetesScriptCommandBuilder WithScriptBody(this ExecuteKubernetesScriptCommandBuilder builder, ScriptBuilder scriptBuilder)
        => (ExecuteKubernetesScriptCommandBuilder)builder.WithScriptBody(scriptBuilder.BuildBashScript());
}