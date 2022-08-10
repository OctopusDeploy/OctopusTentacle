using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Diagnostics.KnowledgeBase
{
    public class ExceptionKnowledge
    {
        readonly Func<Exception, ExceptionKnowledgeBaseEntry?> tryMatch;

        public ExceptionKnowledge(Func<Exception, ExceptionKnowledgeBaseEntry?> tryMatch)
        {
            if (tryMatch == null) throw new ArgumentNullException("tryMatch");
            this.tryMatch = tryMatch;
        }

        public bool TryMatch(Exception exception,
            [NotNullWhen(true)]
            out ExceptionKnowledgeBaseEntry? entry)
        {
            var m = tryMatch(exception);
            if (m == null)
            {
                entry = null;
                return false;
            }

            entry = m;
            return true;
        }
    }
}