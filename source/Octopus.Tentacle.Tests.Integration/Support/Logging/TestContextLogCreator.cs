using System;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Octopus.Tentacle.Tests.Integration.Common.Logging;
using ILog = Halibut.Diagnostics.ILog;

namespace Octopus.Tentacle.Tests.Integration.Support.Logging
{
    public class TestContextLogCreator : ICreateNewILog
    {
        readonly string name;
        readonly LogLevel logLevel;

        public TestContextLogCreator(string name, LogLevel logLevel)
        {
            this.name = name;
            this.logLevel = logLevel;
        }

        public ILog CreateNewForPrefix(string prefix)
        {
            return new TestContextConnectionLog(prefix, name, logLevel);
        }
    }
}