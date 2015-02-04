using System;

namespace Octopus.Shared.Contracts
{
    public class RunProcessCommand
    {
        readonly string executableFilePath;
        readonly string arguments;
        readonly string workingDirectory;

        public RunProcessCommand(string executableFilePath, string arguments, string workingDirectory)
        {
            this.executableFilePath = executableFilePath;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
        }

        public string ExecutableFilePath
        {
            get { return executableFilePath; }
        }

        public string Arguments
        {
            get { return arguments; }
        }

        public string WorkingDirectory
        {
            get { return workingDirectory; }
        }

        public override string ToString()
        {
            return string.Format("{0} {1} (in {2})", executableFilePath, arguments, workingDirectory);
        }
    }
}