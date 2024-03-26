using System;
using System.Text;

namespace Octopus.Tentacle.CommonTestUtils.Builders
{
    public static class StartKubernetesScriptCommandV1AlphaBuilderExtensionMethods
    {
        public static StartKubernetesScriptCommandV1AlphaBuilder WithScriptBodyForCurrentOs(this StartKubernetesScriptCommandV1AlphaBuilder builder, string windowsScript, string bashScript)
        {
            var scriptBody = new StringBuilder(PlatformDetection.IsRunningOnWindows ? windowsScript : bashScript);

            builder.WithScriptBody(scriptBody.ToString());

            return builder;
        }
    }
}
