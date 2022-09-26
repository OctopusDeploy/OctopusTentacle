using System;
using FluentAssertions;
using NUnit.Framework;
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
            var subject = new NamespaceMappingSerializationBinderDecorator(inner, "Octopus.Shared.Contracts", "Octopus.Tentacle.Contracts");
            subject.BindToType("Octopus.Shared", "Octopus.Shared.Contracts.StartScriptCommand");
            inner.TypeName.Should().Be("Octopus.Tentacle.Contracts.StartScriptCommand");
        }

        [Test]
        public void ShouldSerializeToMappedNamespace()
        {
            var inner = new RecordingSerializationBinder();
            var subject = new NamespaceMappingSerializationBinderDecorator(inner, "Octopus.Shared.Contracts", "Octopus.Tentacle.Contracts");
            subject.BindToName(typeof(StartScriptCommand), out _, out var typeName);
            typeName.Should().Be("Octopus.Shared.Contracts.StartScriptCommand");
        }
    }
}