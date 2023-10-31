using System;
using Octopus.Client.Model;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class TentacleOptions : ICommandOptions
    {
        public const int DefaultServerCommsPort = 10943;
        public string Name { get; private set; } = null!;
        public string Policy { get; private set; } = null!;
        public string PublicName { get; private set; } = null!;
        public bool AllowOverwrite { get; private set; }
        string communicationStyle = null!;
        public CommunicationStyle CommunicationStyle { get; private set; } = CommunicationStyle.None;
        public int? ServerCommsPort { get; private set; }
        public string ServerCommsAddress { get; private set; } = null!;
        public string Proxy { get; private set; } = null!;
        public string SpaceName { get; private set; } = null!;
        public string ServerWebSocketAddress { get; private set; } = null!;
        public int? TentacleCommsPort { get; private set; }

        public TentacleOptions(OptionSet options)
        {
            options.Add("name=", "Name of the machine when registered; the default is the hostname", s => Name = s);
            options.Add("policy=", "The name of a machine policy that applies to this machine", s => Policy = s);
            options.Add("h|publicHostName=", "An Octopus-accessible DNS name/IP address for this machine; the default is the hostname", s => PublicName = s);
            options.Add("f|force", "Allow overwriting of existing machines", s => AllowOverwrite = true);
            options.Add("proxy=", "When using passive communication, the name of a proxy that Octopus should connect to the Tentacle through - e.g., 'Proxy ABC' where the proxy name is already configured in Octopus; the default is to connect to the machine directly", s => Proxy = s);
            options.Add("space=", "The name of the space within which this command will be executed. E.g. 'Finance Department' where Finance Department is the name of an existing space. The default space will be used if omitted.", s => SpaceName = s);
            options.Add("server-comms-port=", "When using active communication, the comms port on the Octopus Server; the default is " + DefaultServerCommsPort + ". If specified, this will take precedence over any port number in server-comms-address.", s => ServerCommsPort = int.Parse(s));
            options.Add("server-comms-address=", "When using active communication, the comms address on the Octopus Server; the address of the Octopus Server will be used if omitted.", s => ServerCommsAddress = s);
            options.Add("server-web-socket=", "When using active communication over websockets, the address of the Octopus Server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => ServerWebSocketAddress = s);
            options.Add("tentacle-comms-port=", "When using passive communication, the comms port that the Octopus Server is instructed to call back on to reach this machine; defaults to the configured listening port", s => TentacleCommsPort = int.Parse(s));
            options.Add("comms-style=", "The communication style to use - either TentacleActive or TentaclePassive; the default is " + communicationStyle, s => communicationStyle = s);
        }

        public void Validate()
        {
            if (!Enum.TryParse(communicationStyle, true, out CommunicationStyle style))
                throw new ControlledFailureException("Please specify a valid communications style, e.g. --comms-style=TentaclePassive");

            CommunicationStyle = style;

            if (CommunicationStyle is not CommunicationStyle.TentaclePassive and not CommunicationStyle.TentacleActive)
                throw new ControlledFailureException("Please specify a valid communications style, e.g. TentaclePassive or TentacleActive");

            if (CommunicationStyle == CommunicationStyle.TentacleActive && !string.IsNullOrWhiteSpace(Proxy))
                throw new ControlledFailureException("Option --proxy can only be used with --comms-style=TentaclePassive.  To set a proxy for a polling Tentacle use the polling-proxy command first and then register the Tentacle with register-with.");

            if (!string.IsNullOrEmpty(ServerWebSocketAddress) && !string.IsNullOrEmpty(ServerCommsAddress))
                throw new ControlledFailureException("Please specify a --server-web-socket, or a --server-comms-address - not both.");
        }
    }
}