using System;
using System.Text;

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
    }
}
