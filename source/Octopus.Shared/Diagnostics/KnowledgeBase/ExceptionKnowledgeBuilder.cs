using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Diagnostics.KnowledgeBase
{
    public class ExceptionKnowledgeBuilder
    {
        readonly List<Func<Exception, IDictionary<string, object>, bool>> clauses = new List<Func<Exception, IDictionary<string, object>, bool>>();
        Func<IDictionary<string, object>, string?> entrySummary = s => null;
        Func<IDictionary<string, object>, string?> entryHelpText = s => null;
        Func<IDictionary<string, object>, string?> entryHelpLink = s => null;
        bool logException = true;

        public ExceptionKnowledge Build()
        {
            return new ExceptionKnowledge(ex =>
            {
                if (!clauses.Any()) return null;

                var s = new Dictionary<string, object>();
                foreach (var clause in clauses)
                    if (!clause(ex, s))
                        return null;

                var summary = entrySummary(s);
                if (summary == null)
                    return null;

                return new ExceptionKnowledgeBaseEntry(summary, entryHelpText(s), entryHelpLink(s), logException);
            });
        }

        public ExceptionKnowledgeBuilder ExceptionIs<T>()
            where T : Exception
        {
            return ExceptionIs<T>(ex => true,
                delegate
                {
                });
        }

        public ExceptionKnowledgeBuilder ExceptionIs<T>(Func<T, bool> predicate)
            where T : Exception
        {
            return ExceptionIs(predicate,
                delegate
                {
                });
        }

        public ExceptionKnowledgeBuilder ExceptionIs<T>(Action<T, IDictionary<string, object>> getState)
            where T : Exception
        {
            return ExceptionIs(ex => true, getState);
        }

        public ExceptionKnowledgeBuilder ExceptionIs<T>(Func<T, bool> predicate, Action<T, IDictionary<string, object>> getState)
            where T : Exception
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (getState == null) throw new ArgumentNullException("getState");

            clauses.Add((ex, s) =>
            {
                var tex = ex as T;

                if (tex == null || !predicate(tex))
                    return false;

                getState(tex, s);
                return true;
            });

            return this;
        }

        public ExceptionKnowledgeBuilder HasInnerException<T>()
            where T : Exception
        {
            return HasInnerException<T>(ex => true,
                delegate
                {
                });
        }

        public ExceptionKnowledgeBuilder HasInnerException<T>(Func<T, bool> predicate)
            where T : Exception
        {
            return HasInnerException(predicate,
                delegate
                {
                });
        }

        public ExceptionKnowledgeBuilder HasInnerException<T>(Action<T, IDictionary<string, object>> getState)
            where T : Exception
        {
            return HasInnerException(ex => true, getState);
        }

        public ExceptionKnowledgeBuilder HasInnerException<T>(Func<T, bool> predicate, Action<T, IDictionary<string, object>> getState)
            where T : Exception
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (getState == null) throw new ArgumentNullException("getState");

            clauses.Add((ex, s) =>
            {
                if (ex.InnerException == null)
                    return false;

                foreach (var inner in Enumerate(ex.InnerException))
                {
                    var tex = inner as T;

                    if (tex == null || !predicate(tex))
                        continue;

                    getState(tex, s);
                    return true;
                }

                return false;
            });

            return this;
        }

        IEnumerable<Exception> Enumerate(Exception exception)
        {
            yield return exception;

            var ag = exception as AggregateException;
            if (ag != null)
            {
                foreach (var innerException in ag.InnerExceptions)
                foreach (var ex in Enumerate(innerException))
                    yield return ex;
            }
            else
            {
                if (exception.InnerException != null)
                    foreach (var ex in Enumerate(exception.InnerException))
                        yield return ex;
            }
        }

        public ExceptionKnowledgeBuilder EntrySummaryIs(string summary)
        {
            return EntrySummaryIs(s => summary);
        }

        public ExceptionKnowledgeBuilder EntrySummaryIs(Func<IDictionary<string, object>, string> getSummary)
        {
            entrySummary = getSummary;
            return this;
        }

        public ExceptionKnowledgeBuilder EntryHelpTextIs(string help)
        {
            return EntryHelpTextIs(s => help);
        }

        public ExceptionKnowledgeBuilder SuppressException()
        {
            logException = false;
            return this;
        }

        public ExceptionKnowledgeBuilder EntryHelpTextIs(Func<IDictionary<string, object>, string> getHelp)
        {
            entryHelpText = getHelp;
            return this;
        }

        public ExceptionKnowledgeBuilder EntryHelpLinkIs(string link)
        {
            return EntryHelpLinkIs(s => link);
        }

        public ExceptionKnowledgeBuilder EntryHelpLinkIs(Func<IDictionary<string, object>, string> getLink)
        {
            entryHelpLink = getLink;
            return this;
        }
    }
}
