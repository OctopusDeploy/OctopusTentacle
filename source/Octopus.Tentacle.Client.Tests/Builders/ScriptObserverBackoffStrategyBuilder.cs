using NSubstitute;
using Octopus.Tentacle.Client.Scripts;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class ScriptObserverBackoffStrategyBuilder
    {
        public IScriptObserverBackoffStrategy Build()
        {
            return Substitute.For<IScriptObserverBackoffStrategy>();
        }

        public static IScriptObserverBackoffStrategy Default() => new ScriptObserverBackoffStrategyBuilder().Build();
    }
}