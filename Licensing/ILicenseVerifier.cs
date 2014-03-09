using System;
using System.Xml.Linq;

namespace Octopus.Shared.Licensing
{
    public interface ILicenseVerifier
    {
        bool VerifySignature(XDocument document);
    }
}