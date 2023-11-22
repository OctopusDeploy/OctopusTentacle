using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Tentacle.Util
{
    public class CmdResult
    {
        readonly List<string> infos;
        readonly List<string> errors;

        public CmdResult(int exitCode, List<string> infos, List<string> errors)
        {
            ExitCode = exitCode;
            this.infos = infos;
            this.errors = errors;
        }

        public int ExitCode { get; }

        public IList<string> Infos => infos;
        public IList<string> Errors => errors;

        public void Validate()
        {
            if (ExitCode != 0)
                throw new CommandLineException(ExitCode, errors);
        }
    }

    public class CommandLineException : Exception
    {
        readonly int exitCode;

        public CommandLineException(int exitCode, List<string> errors)
        {
            this.exitCode = exitCode;
            Errors = errors;
        }

        public List<string> Errors { get; }

        public override string Message
        {
            get
            {
                var sb = new StringBuilder(base.Message);

                sb.AppendFormat(" Exit code: {0}", exitCode);
                if (Errors.Count > 0)
                    sb.AppendLine(string.Join(Environment.NewLine, Errors));
                return sb.ToString();
            }
        }
    }
}