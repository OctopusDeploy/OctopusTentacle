using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Octopus.Tentacle.Core.Diagnostics
{
    public static class ExceptionExtensionMethods
    {
        static readonly IDictionary<Type, object> CustomExceptionTypeHandlers = new Dictionary<Type, object>();

        static ExceptionExtensionMethods()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name!.StartsWith("Octopus.")).ToArray();
            var typeInfosToCheck = assemblies
                .SelectMany(a => a.DefinedTypes)
                .ToArray();
            var handlers = typeInfosToCheck
                .Where(t => !t.IsInterface && !t.IsAbstract && t.IsClosedGenericOfType(typeof(ICustomPrettyPrintHandler<>)))
                .ToArray();

            foreach (var handler in handlers)
            {
                var instance = Activator.CreateInstance(handler)!;
                if (instance == null)
                    throw new ArgumentException($"Unable to create PrettyPrint handler of type {handler.FullName}");

                var exceptionTypes = handler.ClosedGenericOfExceptionTypes(typeof(ICustomPrettyPrintHandler<>))
                    .ToArray();
                foreach (var exceptionType in exceptionTypes)
                    CustomExceptionTypeHandlers[exceptionType] = instance;
            }
        }

        public static string PrettyPrint(this Exception ex, bool printStackTrace = true)
        {
            var sb = new StringBuilder();
            PrettyPrint(sb, ex, printStackTrace);
            return sb.ToString().Trim();
        }

        static void PrettyPrint(StringBuilder sb, Exception ex, bool printStackTrace)
        {
            PrettyPrintInternal(sb, (dynamic)ex, printStackTrace);
        }

        static void PrettyPrintInternal<TException>(StringBuilder sb, TException ex, bool printStackTrace)
            where TException : Exception
        {
            if (ex is AggregateException aex)
            {
                AppendAggregateException(sb, printStackTrace, aex);
                return;
            }

            if (ex is OperationCanceledException)
            {
                sb.AppendLine("The task was canceled");
                return;
            }

            var handler = HandlerForType<TException>();
            if (handler != null)
            {
                if (handler.Handle(sb, ex) == false)
                    return;
            }
            else
            {
                sb.AppendLine(ex.Message);
            }

            if (printStackTrace)
                AddStackTrace(sb, ex);

            if (ex.InnerException == null)
                return;

            if (printStackTrace)
                sb.AppendLine("--Inner Exception--");

            PrettyPrint(sb, ex.InnerException, printStackTrace);
        }

        static ICustomPrettyPrintHandler<TException>? HandlerForType<TException>()
            where TException : Exception
        {
            var exceptionTypeInfo = typeof(TException).GetTypeInfo();
            var key = CustomExceptionTypeHandlers.Keys.FirstOrDefault(k => k.GetTypeInfo().IsAssignableFrom(exceptionTypeInfo));
            if (key == null)
                return null;
            var handler = CustomExceptionTypeHandlers[key];
            return (ICustomPrettyPrintHandler<TException>)handler;
        }

        static void AppendAggregateException(StringBuilder sb, bool printStackTrace, AggregateException aex)
        {
            if (!printStackTrace && aex.InnerExceptions.Count == 1 && aex.InnerException != null)
            {
                PrettyPrint(sb, aex.InnerException, printStackTrace);
            }
            else
            {
                sb.AppendLine("Aggregate Exception");
                if (printStackTrace)
                    AddStackTrace(sb, aex);
                for (var x = 0; x < aex.InnerExceptions.Count; x++)
                {
                    sb.AppendLine($"--Inner Exception {x + 1}--");
                    PrettyPrint(sb, aex.InnerExceptions[x], printStackTrace);
                }
            }
        }

        static void AddStackTrace(StringBuilder sb, Exception ex)
        {
            if (ex is ReflectionTypeLoadException rtle)
                AddReflectionTypeLoadExceptionDetails(rtle, sb);

            sb.AppendLine(ex.GetType().FullName);
            try
            {
                sb.AppendLine(StackTraceHelper.GetCleanStackTrace(ex));
            }
            catch // Sometimes fails printing the trace
            {
                sb.AppendLine(ex.StackTrace);
            }
        }

        static void AddReflectionTypeLoadExceptionDetails(ReflectionTypeLoadException rtle, StringBuilder sb)
        {
            if (rtle.LoaderExceptions == null)
                return;

            foreach (var loaderException in rtle.LoaderExceptions)
            {
                if (loaderException == null)
                    continue;

                sb.AppendLine();
                sb.AppendLine("--Loader Exception--");
                PrettyPrint(sb, loaderException, true);

#if !NETSTANDARD1_0
                var fusionLog = (loaderException as FileNotFoundException)?.FusionLog;
                if (!string.IsNullOrEmpty(fusionLog))
                    sb.Append("Fusion log: ").AppendLine(fusionLog);
#endif
            }
        }

        static class StackTraceHelper
        {
            //VBConversions Note: Former VB static variables moved to class level because they aren't supported in C#.
            static readonly Regex Re1 = new Regex("VB\\$StateMachine_[\\d]+_(.+)\\.MoveNext\\(\\)");
            static readonly Regex Re2 = new Regex("<([^>]+)>[^.]+\\.MoveNext\\(\\)");
            static readonly Regex Re3 = new Regex("^(.*) in (.*):line ([0-9]+)$");

            public static string GetCleanStackTrace(Exception ex)
            {
                if (ex.StackTrace == null)
                    return "";

                var sb = new StringBuilder();

                foreach (var stackTrace in ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var s = stackTrace;

                    // Get rid of stack-frames that are part of the BCL async machinery
                    if (s.StartsWith("   at "))
                        s = s.Substring(6);
                    else
                        continue;

                    if (s == "System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)")
                        continue;

                    if (s == "System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task)")
                        continue;

                    if (s == "System.Runtime.CompilerServices.TaskAwaiter`1.GetResult()")
                        continue;

                    if (s == "System.Runtime.CompilerServices.TaskAwaiter.GetResult()")
                        continue;

                    // Get rid of stack-frames that are part of the runtime exception handling machinery
                    if (s == "System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()")
                        continue;

                    // Get rid of stack-frames that are part of .NET Native machiner
                    if (s.Contains("!<BaseAddress>+0x"))
                        continue;

                    // Get rid of stack frames from VB and C# compiler-generated async state machine
                    s = Re1.Replace(s, "$1");
                    s = Re2.Replace(s, "$1");

                    // If the stack trace had PDBs, "Alpha.Beta.GammaAsync in c:\code\module1.vb:line 53"
                    var re3Match = Re3.Match(s);
                    s = re3Match.Success ? re3Match.Groups[1].Value : s;
                    var pdbfile = re3Match.Success ? re3Match.Groups[2].Value : null;
                    var pdbline = re3Match.Success ? (int?)int.Parse(Convert.ToString(re3Match.Groups[3].Value)) : null;

                    // Get rid of stack frames from AsyncStackTrace
                    if (s.EndsWith("AsyncStackTraceExtensions.Log`1"))
                        continue;

                    if (s.EndsWith("AsyncStackTraceExtensions.Log"))
                        continue;

                    if (s.Contains("AsyncStackTraceExtensions.Log<"))
                        continue;

                    var fullyQualified = s;

                    if (pdbfile != null)
                        sb.AppendFormat("   at {1} in {2}:line {3}{0}",
                            Environment.NewLine,
                            fullyQualified,
                            Path.GetFileName(pdbfile),
                            pdbline);
                    else
                        sb.AppendFormat("   at {1}{0}", "\r\n", fullyQualified);
                }

                return sb.ToString();
            }
        }
    }
}