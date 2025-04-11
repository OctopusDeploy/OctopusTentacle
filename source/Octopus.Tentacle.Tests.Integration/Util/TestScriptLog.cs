using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Services.Scripts.Logging;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class TestScriptLog : IScriptLog, IScriptLogWriter
    {
        public readonly StringBuilder Debug = new StringBuilder();
        public readonly StringBuilder StdOut = new StringBuilder();
        public readonly StringBuilder StdErr = new StringBuilder();

        public IScriptLogWriter CreateWriter()
            => this;

        public List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber)
            => throw new NotImplementedException();

        public void Dispose()
        {
        }

        public void WriteOutput(ProcessOutputSource source, string message)
            => WriteOutput(source, message, DateTimeOffset.UtcNow);

        public void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred)
        {
            Console.WriteLine($"{occurred} {source} {message}");
            switch (source)
            {
                case ProcessOutputSource.Debug:
                    Debug.AppendLine(message);
                    break;

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