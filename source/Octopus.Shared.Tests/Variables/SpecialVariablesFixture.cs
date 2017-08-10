using NUnit.Framework;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Tests.Variables
{
    [TestFixture]
    public class SpecialVariablesFixture
    {
        [Test]
        public void SpecialVariableDefinitionsShouldLoad()
        {
            var specialVariableDefinitions = SpecialVariables.Definitions.Value;

            foreach (var specialVariableDefinition in specialVariableDefinitions)
            {
                Assert.That(specialVariableDefinition.Name, Is.Not.Empty);
            }
        }
    }
}