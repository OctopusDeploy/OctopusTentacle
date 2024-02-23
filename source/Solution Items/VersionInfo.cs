// This file will be updated by our build process at compile time.
using System;
using System.Reflection;

[assembly: AssemblyVersion("8.1.938")]
[assembly: AssemblyFileVersion("8.1.938")]
[assembly: AssemblyInformationalVersion("8.1.938-ap-pods-not-jobs+Branch.ap-pods-not-jobs.Sha.9f4edd246f37fd377ea853cf4a52719d6c7739e7")]
[assembly: AssemblyGitBranch("")]
[assembly: AssemblyNuGetVersion("8.1.938-ap-pods-not-jobs-20240223122851")]

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