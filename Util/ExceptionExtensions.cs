using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Octopus.Shared.Activities;

namespace Octopus.Shared.Util
{
    public static class ExceptionExtensions
    {
        public static Exception GetRootError(this Exception error)
        {
            if (error is AggregateException)
            {
                foreach (var item in ((AggregateException)error).InnerExceptions)
                {
                    return GetRootError(item);
                }
            }

            if (error is TargetInvocationException && error.InnerException != null)
            {
                return GetRootError(error.InnerException);
            }

            return error;
        }

        public static string SuggestSolution(this HttpListenerException error, IList<Uri> prefixes)
        {
            if (error.ErrorCode != 5)
                return null;

            var message = new StringBuilder();
            message.AppendFormat("The service was unable to start because the HttpListener returned an error: {0}", error.Message).AppendLine();
            message.AppendLine("This could occur for one of two reasons. First, ensure that the current user has access to listen on these prefixes by running the following command(s):");
            foreach (var prefix in prefixes)
            {
                message.AppendFormat("  netsh http add urlacl url={0}://+:{1}{2} user={3}\\{4}",
                    prefix.Scheme,
                    prefix.Port,
                    prefix.PathAndQuery,
                    Environment.UserDomainName,
                    Environment.UserName)
                    .AppendLine();
            }

            message.Append("Alternatively, this error might have been caused by one of the ports being in use by another process. Ensure that no other processes are listening on the TCP port(s): ")
                .Append(string.Join(", ", prefixes.Select(p => p.Port)));

            return message.ToString();
        }

        public static string GetErrorSummary(this Exception error)
        {
            error = error.GetRootError();

            if (error is TaskCanceledException)
                return "The task was canceled.";

            if (error is ActivityFailedException)
                return error.Message;

            return error.Message;
        }
    }
}