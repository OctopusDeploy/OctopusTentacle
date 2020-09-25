using System;

namespace Octopus.Tentacle.Versioning
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class AssemblyGitBranchAttribute : Attribute
    {
        public AssemblyGitBranchAttribute(string branchName)
        {
            BranchName = branchName;
        }

        public string BranchName { get; }
    }
}