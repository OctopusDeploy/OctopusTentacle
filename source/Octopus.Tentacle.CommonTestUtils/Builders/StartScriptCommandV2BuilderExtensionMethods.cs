using System;
using System.Text;
using Octopus.Tentacle.Contracts.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.CommonTestUtils.Builders
{
    public static class StartScriptCommandV2BuilderExtensionMethods
    {
        public static StartScriptCommandV2Builder WithScriptBodyForCurrentOs(this StartScriptCommandV2Builder builder, string windowsScript, string bashScript)
        {
            var scriptBody = new StringBuilder(PlatformDetection.IsRunningOnWindows ? windowsScript : bashScript);

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }

        public static StartScriptCommandV2Builder WithScriptBody(this StartScriptCommandV2Builder builder, ScriptBuilder scriptBuilder)
        {
            var scriptBody = new StringBuilder(scriptBuilder.BuildForCurrentOs());

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }

        public static StartScriptCommandV2Builder WithScriptBody(this StartScriptCommandV2Builder builder, Action<ScriptBuilder> builderFunc)
        {
            var scriptBuilder = new ScriptBuilder();
            builderFunc(scriptBuilder);

            return builder.WithScriptBody(scriptBuilder);
        }
    }
}
