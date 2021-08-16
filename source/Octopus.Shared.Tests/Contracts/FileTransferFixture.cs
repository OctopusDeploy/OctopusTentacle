using System;
using System.Security.Cryptography.X509Certificates;
using Halibut;
using Halibut.Transport.Protocol;
using NUnit.Framework;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Tests.Contracts
{
    public class FileTransferFixture
    {
        [Test]
        public void IFileTransferService_HasValidTypes()
        {
            // Constructing binder verifies that the interface does not have any disallowed types
            var binder = new RegisteredSerializationBinder(new[] { typeof(IFileTransferService) });
        }
    }
}