using System;
using System.Collections.Generic;

namespace Octopus.Shared.ServiceMessages
{
    public class ParseServiceMessageResult
    {
        readonly string text;
        readonly List<ServiceMessage> matches;

        public ParseServiceMessageResult(string text, List<ServiceMessage> matches)
        {
            this.text = text;
            this.matches = matches ?? new List<ServiceMessage>();
        }

        public string Text
        {
            get { return text; }
        }

        public IList<ServiceMessage> Matches
        {
            get { return matches; }
        }
    }
}