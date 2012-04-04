using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public enum PackageExistance
    {
        [EnumMember] AlreadyUploaded,
        [EnumMember] NotUploaded
    }
}