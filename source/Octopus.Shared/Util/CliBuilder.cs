using System;
using System.Collections.Generic;
using System.Globalization;

namespace Octopus.Shared.Util
{
    public class CliBuilder
    {
        readonly string executable;
        readonly List<string> arguments = new List<string>();
        readonly List<string> systemArguments = new List<string>();
        string? action;
        bool ignoreFailedExitCode;

        public CliBuilder(string executable)
        {
            if (executable == null) throw new ArgumentNullException("executable");
            this.executable = executable;
        }

        public static CliBuilder ForTool(string executable, string action, string instance)
            => new CliBuilder(executable).Action(action).Console().NoLogo().Instance(instance);

        public static CliBuilder StopService(string executable, string instance)
            => ForTool(executable, "service", instance).Flag("stop");

        public static CliBuilder StartService(string executable, string instance)
            => ForTool(executable, "service", instance).Flag("start");

        public static CliBuilder RestartService(string executable, string instance)
            => StopService(executable, instance).Flag("start");

        public CliBuilder Action(string actionName)
        {
            if (actionName == null) throw new ArgumentNullException("actionName");
            if (action != null) throw new InvalidOperationException("Action is already set");
            action = Normalize(actionName);
            return this;
        }

        public CliBuilder IgnoreFailedExitCode()
        {
            ignoreFailedExitCode = true;
            return this;
        }

        public CliBuilder Flag(string flagName)
        {
            arguments.Add(MakeFlag(flagName));
            return this;
        }

        public CliBuilder Flag(string flagName, bool condition)
        {
            if (condition)
                arguments.Add(MakeFlag(flagName));
            return this;
        }

        public CliBuilder SystemFlag(string flagName)
        {
            systemArguments.Add(MakeFlag(flagName));
            return this;
        }

        public CliBuilder Console()
            // Omitting this breaks remote script runs, so we may want to include it in exported scripts;
            // adding it does make a lot of clutter though.
            => SystemFlag("console");

        public CliBuilder NoLogo()
            => SystemFlag("nologo");

        public CliBuilder Instance(string instance)
            => Argument("instance", instance);

        string MakeFlag(string flagName)
            => "--" + Normalize(flagName);

        public CliBuilder PositionalArgument(object argValue)
        {
            arguments.Add(MakePositionalArg(argValue));
            return this;
        }

        public CliBuilder Argument(string argName, object argValue, bool unescaped = false)
        {
            arguments.Add(MakeArg(argName, argValue));
            return this;
        }

        public CliBuilder SystemArgument(string argName, object argValue)
        {
            systemArguments.Add(MakeArg(argName, argValue));
            return this;
        }

        static string MakePositionalArg(object argValue)
        {
            var sval = "";
            var f = argValue as IFormattable;
            if (f != null)
                sval = f.ToString(null, CultureInfo.InvariantCulture);
            else if (argValue != null)
                sval = argValue.ToString();

            return string.Format("{0}", Escape(sval ?? ""));
        }

        static string MakeArg(string argName, object argValue, bool unescaped = false)
        {
            var sval = "";
            var f = argValue as IFormattable;
            if (f != null)
                sval = f.ToString(null, CultureInfo.InvariantCulture);
            else if (argValue != null)
                sval = argValue.ToString();

            return string.Format("--{0} {1}", Normalize(argName), unescaped ? sval : Escape(sval ?? ""));
        }

        public static string Escape(string argValue)
        {
            if (argValue == null) throw new ArgumentNullException("argValue");

            // Though it isn't aesthetically pleasing, we always return a double-quoted
            // value.

            var last = argValue.Length - 1;
            var preq = true;
            while (last >= 0)
            {
                // Escape backslashes only when they're butted up against the
                // end of the value, or an embedded double quote

                var cur = argValue[last];
                if (cur == '\\' && preq)
                    argValue = argValue.Insert(last, "\\");
                else if (cur == '"')
                    preq = true;
                else
                    preq = false;
                last -= 1;
            }

            // Double-quotes are always escaped.
            return "\"" + argValue.Replace("\"", "\\\"") + "\"";
        }

        static string Normalize(string s)
        {
            if (s == null) throw new ArgumentNullException("s");
            return s.Trim();
        }

        public CommandLineInvocation Build()
        {
            var argLine = new List<string>();
            if (action != null)
                argLine.Add(action);
            argLine.AddRange(arguments);

            return new CommandLineInvocation(executable, string.Join(" ", argLine), string.Join(" ", systemArguments))
            {
                IgnoreFailedExitCode = ignoreFailedExitCode
            };
        }
    }
}