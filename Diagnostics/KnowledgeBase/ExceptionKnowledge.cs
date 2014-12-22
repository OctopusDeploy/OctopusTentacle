using System;

namespace Octopus.Shared.Diagnostics.KnowledgeBase
{
    class ExceptionKnowledge
    {
        readonly Func<Exception, ExceptionKnowledgeBaseEntry> tryMatch;

        public ExceptionKnowledge(Func<Exception, ExceptionKnowledgeBaseEntry> tryMatch)
        {
            if (tryMatch == null) throw new ArgumentNullException("tryMatch");
            this.tryMatch = tryMatch;
        }

        public bool TryMatch(Exception exception, out ExceptionKnowledgeBaseEntry entry)
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