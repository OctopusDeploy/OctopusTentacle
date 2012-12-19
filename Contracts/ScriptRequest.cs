using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class ScriptRequest
    {
        [DataMember]
        public string Script { get; set; }

        [DataMember]
        public List<Variable> Variables { get; set; }
    }
}