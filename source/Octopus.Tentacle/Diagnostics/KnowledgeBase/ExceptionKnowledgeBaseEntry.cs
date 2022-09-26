using System;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Octopus.Tentacle.Diagnostics.KnowledgeBase
{
    public class ExceptionKnowledgeBaseEntry
    {
        public ExceptionKnowledgeBaseEntry(string summary, string? helpText, string? helpLink, bool logException)
        {
            HelpLink = helpLink;
            LogException = logException;
            HelpText = helpText;
            Summary = summary;
        }

        public string Summary { get; }
        public string? HelpText { get; }
        public string? HelpLink { get; }
        public bool LogException { get; }

        public override string ToString()
        {
            var parts = new[] { Summary, HelpText ?? string.Empty }.WhereNotNullOrWhiteSpace().ToList();
            if (HelpLink != null)
                parts.Add("See: " + HelpLink);

            return string.Join(" ", parts);
        }
    }
}