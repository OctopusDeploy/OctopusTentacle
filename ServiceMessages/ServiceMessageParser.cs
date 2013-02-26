using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Octopus.Shared.ServiceMessages
{
    public class ServiceMessageParser : IServiceMessageParser
    {
        static readonly Regex Regex = new Regex(@"\#\#octopus\s*?\[(?<message>.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        public ParseServiceMessageResult ParseServiceMessages(string text)
        {
            var matches = new List<ServiceMessage>();

            text = Regex.Replace(text, delegate(Match match)
            {
                var messageText = match.Groups["message"];

                matches.Add(ParseMessage(messageText.Value));

                return string.Empty;
            });

            return new ParseServiceMessageResult(text, matches);
        }

        static ServiceMessage ParseMessage(string messageText)
        {
            var element = XElement.Parse("<" + messageText + "/>");
            var name = element.Name.LocalName;
            var values = element.Attributes().ToDictionary(s => s.Name.LocalName, s => Encoding.UTF8.GetString(Convert.FromBase64String(s.Value)), StringComparer.OrdinalIgnoreCase);
            return new ServiceMessage(name, values);
        }
    }
}