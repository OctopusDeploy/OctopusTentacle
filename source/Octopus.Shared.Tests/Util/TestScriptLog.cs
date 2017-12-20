using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Shared.Contracts;
using Octopus.Shared.Scripts;

namespace Octopus.Shared.Tests.Util
{
    public class TestScriptLog : IScriptLog, IScriptLogWriter
    {
        public readonly StringBuilder StdOut = new StringBuilder();
        public readonly StringBuilder StdErr = new StringBuilder();

        public IScriptLogWriter CreateWriter()
        {
            return this;
        }

        public List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public void WriteOutput(ProcessOutputSource source, string message)
        {
            Console.WriteLine($"{DateTime.UtcNow} {source} {message}");
            switch (source)
            {
                case ProcessOutputSource.StdOut:
                    StdOut.AppendLine(message);
                    break;

                case ProcessOutputSource.StdErr:
                    StdErr.AppendLine(message);
                    break;
            }
        }
    }
}