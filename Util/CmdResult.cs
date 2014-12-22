using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Shared.Util
{
    public class CmdResult
    {
        readonly int exitCode;
        readonly List<string> infos;
        readonly List<string> errors;

        public CmdResult(int exitCode, List<string> infos, List<string> errors)
        {
            this.exitCode = exitCode;
            this.infos = infos;
            this.errors = errors;
        }

        public int ExitCode
        {
            get { return exitCode; }
        }

        public IList<string> Infos
        {
            get { return infos; }
        }

        public IList<string> Errors
        {
            get { return errors; }
        }

        public void Validate()
        {
            if (ExitCode != 0)
            {
                throw new CommandLineException(ExitCode, errors);
            }
        }
    }

    public class CommandLineException : Exception
    {
        readonly int exitCode;
        readonly List<string> errors;

        public CommandLineException(int exitCode, List<string> errors)
        {
            this.exitCode = exitCode;
            this.errors = errors;
        }

        public List<string> Errors
        {
            get { return errors; }
        }

        public override string Message
        {
            get
            {
                var sb = new StringBuilder(base.Message);

                sb.AppendFormat(" Exit code: {0}", exitCode);
                if (Errors.Count > 0)
                {
                    sb.AppendLine(string.Join(Environment.NewLine, Errors));
                }
                return sb.ToString();
            }
        }
    }
}