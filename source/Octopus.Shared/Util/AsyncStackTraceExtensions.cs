using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Octopus.Shared.Util
{
    public static class AsyncStackTraceExtensions
    {
        //VBConversions Note: Former VB static variables moved to class level because they aren't supported in C#.
        static readonly Queue<ExceptionLog> EmptyLog = new Queue<ExceptionLog>();
        static readonly Regex Re1 = new Regex("VB\\$StateMachine_[\\d]+_(.+)\\.MoveNext\\(\\)");
        static readonly Regex Re2 = new Regex("<([^>]+)>[^.]+\\.MoveNext\\(\\)");
        static readonly Regex Re3 = new Regex("^(.*) in (.*):line ([0-9]+)$");
        static readonly Regex Re4 = new Regex("^.*\\.([^.]+)$");

        static void LogInternal(Exception ex, ExceptionLog log)
        {
            if (ex.Data.Contains("AsyncStackTrace"))
            {
                ((Queue<ExceptionLog>)ex.Data["AsyncStackTrace"]!).Enqueue(log);
            }
            else
            {
                Queue<ExceptionLog> logs = new Queue<ExceptionLog>();
                logs.Enqueue(log);
                ex.Data["AsyncStackTrace"] = logs;
            }
        }

        public static string StackTraceEx(this Exception ex)
        {
            var logs = ex.Data.Contains("AsyncStackTrace") ? (Queue<ExceptionLog>)ex.Data["AsyncStackTrace"]! : EmptyLog;
            logs = new Queue<ExceptionLog>(logs);

            var sb = new StringBuilder();

            if (ex.StackTrace != null)
            {
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

                    // Extract the method name, "Alpha.Beta.GammaAsync"
                    var re4Match = Re4.Match(s);
                    var fullyQualified = s;
                    var member = re4Match.Success ? re4Match.Groups[1].Value : "";

                    // Now attempt to relate this to the log
                    // We'll assume that every logged call is in the stack (Q. will this assumption be violated by inlining?)
                    // We'll assume that not every call in the stack was logged, since users might chose not to log
                    // We'll assume that the bottom-most stack frame wasn't logged
                    if (logs.Count > 0 && logs.Peek().Member == member && sb.Length > 0)
                    {
                        var log = logs.Dequeue();
                        sb.AppendFormat("   at {1}{2} in {3}:line {4}{0}",
                            Environment.NewLine,
                            fullyQualified,
                            log.LabelAndArg,
                            Path.GetFileName(log.Path),
                            log.Line);
                    }
                    else if (pdbfile != null)
                    {
                        sb.AppendFormat("   at {1} in {2}:line {3}{0}",
                            Environment.NewLine,
                            fullyQualified,
                            Path.GetFileName(pdbfile),
                            pdbline);
                    }
                    else
                    {
                        sb.AppendFormat("   at {1}{0}", "\r\n", fullyQualified);
                    }
                }
            }

            if (logs.Count > 0 && sb.Length > 0)
                sb.AppendLine("---------------- extra logged stackframes:");

            foreach (var log in logs)
                sb.AppendFormat("   at {1}{2} in {3}:line {4}{0}",
                    Environment.NewLine,
                    log.Member,
                    log.LabelAndArg,
                    Path.GetFileName(log.Path),
                    log.Line);

            return sb.ToString();
        }

        public static async Task<T> Log<T>(this Task<T> task,
            string? label = null,
            object? arg = null,
            [CallerMemberName]
            string member = "",
            [CallerLineNumber]
            int line = 0,
            [CallerFilePath]
            string path = "")
        {
            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                LogInternal(ex, new ExceptionLog { Label = label, Arg = arg != null ? arg.ToString()! : "", Member = member, Line = line, Path = path });
                throw;
            }
        }

        public static async Task Log(this Task task,
            string? label = null,
            object? arg = null,
            [CallerMemberName]
            string member = "",
            [CallerLineNumber]
            int line = 0,
            [CallerFilePath]
            string path = "")
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                LogInternal(ex, new ExceptionLog { Label = label, Arg = arg != null ? arg.ToString()! : "", Member = member, Line = line, Path = path });
                throw;
            }
        }

        class ExceptionLog
        {
            public string? Label;
            public string Arg = string.Empty;
            public string Member = string.Empty;
            public string Path = string.Empty;
            public int Line;

            public string LabelAndArg
            {
                get
                {
                    string returnValue = "";
                    returnValue = "";
                    if (Label != null)
                        returnValue += "#" + Label;
                    if (Label != null && Arg != null)
                        returnValue += "(" + Arg + ")";
                    return returnValue;
                }
            }
        }
    }
}
