// This file will be updated by our build process at compile time.
using System;
using System.Reflection;

[assembly: AssemblyVersion("8.1.1269")]
[assembly: AssemblyFileVersion("8.1.1269")]
[assembly: AssemblyInformationalVersion("8.1.1269-ap-testing-k8s-agent+Branch.ap-testing-k8s-agent.Sha.1c27b2a7829667ffbf9739d2baa846633bea15df")]
[assembly: AssemblyGitBranch("")]
[assembly: AssemblyNuGetVersion("8.1.1269-ap-testing-k8s-agent-20240412120751")]

#if DEFINE_VERSION_ATTRIBUTES
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class AssemblyGitBranchAttribute : Attribute
    {
        public AssemblyGitBranchAttribute(string branchName)
        {
            BranchName = branchName;
        }

        public string BranchName { get; }
    }

    public sealed class AssemblyNuGetVersionAttribute : Attribute
    {
        public AssemblyNuGetVersionAttribute(string nuGetVersion)
        {
            NuGetVersion = nuGetVersion;
        }

        public string NuGetVersion { get; }
    }
#endif