using System;
using System.Text;
using Octopus.Tentacle.CommonTestUtils;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public static class ExecuteScriptCommandBuilderExtensionMethods
    {
        public static TestExecuteShellScriptCommandBuilder SetScriptBodyForCurrentOs(this TestExecuteShellScriptCommandBuilder builder, string windowsScript, string bashScript)
        {
            var scriptBody = new StringBuilder(PlatformDetection.IsRunningOnWindows ? windowsScript : bashScript);

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }

        public static TestExecuteShellScriptCommandBuilder SetScriptBody(this TestExecuteShellScriptCommandBuilder builder, ScriptBuilder scriptBuilder)
        {
            var scriptBody = new StringBuilder(scriptBuilder.BuildForCurrentOs());

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }

        public static TestExecuteShellScriptCommandBuilder SetScriptBody(this TestExecuteShellScriptCommandBuilder builder, Action<ScriptBuilder> builderFunc)
        {
            var scriptBuilder = new ScriptBuilder();
            builderFunc(scriptBuilder);

            return builder.SetScriptBody(scriptBuilder);
        }
    }
}
