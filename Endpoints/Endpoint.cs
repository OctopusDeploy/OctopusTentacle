using System;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Client.Model;

namespace Octopus.Shared.Endpoints
{
    public abstract class Endpoint
    {
        [JsonIgnore]
        public abstract CommunicationStyle CommunicationStyle { get; }

        [JsonIgnore]
        public bool ScriptConsoleSupported {
            get
            {
                // If the CommunicationStyle is decorated with a ScriptConsoleSupportedAttribute, then the endpoint supports running scripts via the console
                return (typeof (CommunicationStyle).GetField(CommunicationStyle.ToString())).GetCustomAttributes(typeof (ScriptConsoleSupportedAttribute), false).Any();
            } 
        }

        [JsonIgnore]
        public bool TentacleUpgradeSupported
        {
            get
            {
                // If the CommunicationStyle is decorated with a TentacleUpgradeSupportedAttribute, then the endpoint may contain a tentacle that requires upgrading
                return (typeof(CommunicationStyle).GetField(CommunicationStyle.ToString())).GetCustomAttributes(typeof(TentacleUpgradeSupportedAttribute), false).Any();
            }
        }
    }
}
