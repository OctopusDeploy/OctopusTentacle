using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Builders;

namespace Octopus.Tentacle.CommonTestUtils.Builders
{
    public class LatestStartScriptCommandBuilder : StartScriptCommandV3AlphaBuilder
    {
        public LatestStartScriptCommandBuilder()
        {
            WithIsolation(ScriptIsolationLevel.NoIsolation);
        }
    }
}