using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public enum JobState
    {
        [EnumMember] Queued,
        [EnumMember] Executing,
        [EnumMember] Success,
        [EnumMember] Error,
        [EnumMember] NotFound
    }
}