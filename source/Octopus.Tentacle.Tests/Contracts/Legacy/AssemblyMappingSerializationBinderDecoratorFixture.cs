using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;

namespace Octopus.Tentacle.Tests.Communications
{
    [TestFixture]
    public class AssemblyMappingSerializationBinderDecoratorFixture
    {
        [Test]
        public void ShouldDeserializeToMappedAssembly()
        {
            var inner = new RecordingSerializationBinder();
            var subject = new AssemblyMappingSerializationBinderDecorator(inner, "Octopus.Shared", "Octopus.Tentacle.Contracts");
            subject.BindToType("Octopus.Shared", "Octopus.Shared.Contracts.StartScriptCommand");
            inner.AssemblyName.Should().Be("Octopus.Tentacle.Contracts");
        }

        [Test]
        public void ShouldSerializeToMappedAssembly()
        {
            var inner = new RecordingSerializationBinder();
            var subject = new AssemblyMappingSerializationBinderDecorator(inner, "Octopus.Shared", "Octopus.Tentacle.Contracts");
            subject.BindToName(typeof(StartScriptCommand), out var assemblyName, out _);
            assemblyName.Should().StartWith("Octopus.Shared");
        }
    }
}