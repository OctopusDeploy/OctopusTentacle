using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Octopus.Shared.ServiceMessages
{
    public class ServiceMessageParser
    {
        private readonly Action<string> stdOut;
        private readonly Action<ServiceMessage> serviceMessage;
        readonly StringBuilder buffer = new StringBuilder();
        State state = State.Default;

        public ServiceMessageParser(Action<string> stdOut, Action<ServiceMessage> serviceMessage)
        {
            this.stdOut = stdOut;
            this.serviceMessage = serviceMessage;
        }

        public void Append(string line)
        {
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                switch (state)
                {
                    case State.Default:
                        if (c == '\r')
                        {
                        }
                        else if (c == '\n')
                        {
                            Flush(stdOut);
                        }
                        else if (c == '#')
                        {
                            Flush(stdOut);
                            state = State.PossibleMessage;
                            buffer.Append(c);
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;

                    case State.PossibleMessage:
                        buffer.Append(c);
                        var progress = buffer.ToString();
                        if ("##octopus" == progress)
                        {
                            state = State.InMessage;
                            buffer.Clear();
                        }
                        else if (!"##octopus".StartsWith(progress))
                        {
                            state = State.Default;
                        }
                        break;
                    
                    case State.InMessage:
                        if (c == ']')
                        {
                            Flush(ProcessMessage);
                            state = State.Default;
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Finish()
        {
            if (buffer.Length > 0)
            {
                Flush(stdOut);
            }
        }

        void ProcessMessage(string message)
        {
            message = message.Trim().TrimStart('[');

            var element = XElement.Parse("<" + message + "/>");
            var name = element.Name.LocalName;
            var values = element.Attributes().ToDictionary(s => s.Name.LocalName, s => Encoding.UTF8.GetString(Convert.FromBase64String(s.Value)), StringComparer.OrdinalIgnoreCase);
            serviceMessage(new ServiceMessage(name, values));
        }

        void Flush(Action<string> to)
        {
            var result = buffer.ToString();
            buffer.Clear();

            if (result.Length > 0)
                to(result);
        }

        enum State
        {
            Default,
            PossibleMessage,
            InMessage
        }
    }
}