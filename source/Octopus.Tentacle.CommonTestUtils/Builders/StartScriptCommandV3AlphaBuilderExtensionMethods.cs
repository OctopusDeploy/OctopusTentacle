using System;
using System.Text;
using Octopus.Tentacle.Contracts.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.CommonTestUtils.Builders
{
    public static class StartScriptCommandV3AlphaBuilderExtensionMethods
    {
        public static StartScriptCommandV3AlphaBuilder WithScriptBodyForCurrentOs(this StartScriptCommandV3AlphaBuilder builder, string windowsScript, string bashScript)
        {
            var scriptBody = new StringBuilder(PlatformDetection.IsRunningOnWindows ? windowsScript : bashScript);

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }

        public static StartScriptCommandV3AlphaBuilder WithScriptBody(this StartScriptCommandV3AlphaBuilder builder, ScriptBuilder scriptBuilder)
        {
            var scriptBody = new StringBuilder(scriptBuilder.BuildForCurrentOs());

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }

        public static StartScriptCommandV3AlphaBuilder WithScriptBody(this StartScriptCommandV3AlphaBuilder builder, Action<ScriptBuilder> builderFunc)
        {
            var scriptBuilder = new ScriptBuilder();
            builderFunc(scriptBuilder);

            return builder.WithScriptBody(scriptBuilder);
        }
    }
}
