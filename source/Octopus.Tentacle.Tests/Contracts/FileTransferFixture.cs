using System;
using Halibut.Transport.Protocol;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Contracts
{
    public class FileTransferFixture
    {
        [Test]
        public void IFileTransferService_HasValidTypes()
        {
            // Constructing binder verifies that the interface does not have any disallowed types
            var binder = new RegisteredSerializationBinder();
            binder.Register(typeof(IFileTransferService));
        }
    }
}