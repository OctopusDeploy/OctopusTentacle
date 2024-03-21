using System;
using System.Text;

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
    }
}
