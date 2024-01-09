using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;

namespace Octopus.Tentacle.Tests.Communications
{
    [TestFixture]
    public class NamespaceMappingSerializationBinderDecoratorFixture
    {
        [Test]
        public void ShouldDeserializeToMappedNamespace()
        {
            var inner = new RecordingSerializationBinder();
            var fromNamespace = "Octopus.Shared.Contracts";
            var toNamespace = "Octopus.Tentacle.Contracts";
            var subject = new NamespaceMappingSerializationBinderDecorator(inner, fromNamespace, toNamespace, new ReMappedLegacyTypes(fromNamespace, toNamespace));
            subject.BindToType("Octopus.Shared", "Octopus.Shared.Contracts.StartScriptCommand");
            inner.TypeName.Should().Be("Octopus.Tentacle.Contracts.StartScriptCommand");
        }

        [Test]
        public void ShouldSerializeToMappedNamespace()
        {
            var inner = new RecordingSerializationBinder();
            var fromNamespace = "Octopus.Shared.Contracts";
            var toNamespace = "Octopus.Tentacle.Contracts";
            
            var subject = new NamespaceMappingSerializationBinderDecorator(inner, fromNamespace, toNamespace, new ReMappedLegacyTypes(fromNamespace, toNamespace));
            subject.BindToName(typeof(StartScriptCommand), out _, out var typeName);
            typeName.Should().Be("Octopus.Shared.Contracts.StartScriptCommand");
        }
    }
}