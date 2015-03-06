using System;
using Octopus.Client.Model;

namespace Octopus.Shared.Endpoints
{
    public class OfflineDropEndpoint : Endpoint
    {
        public override CommunicationStyle CommunicationStyle
        {
            get { return CommunicationStyle.OfflineDrop; }
        }

        public string DropFolderPath { get; set; }

        public override string ToString()
        {
            return DropFolderPath ?? "(none)";
        }
    }
}
