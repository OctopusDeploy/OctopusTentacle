// This file will be updated by our build process at compile time.

using System;
using System.Reflection;

[assembly: AssemblyVersion("6.0.35")]
[assembly: AssemblyFileVersion("6.0.35")]
[assembly: AssemblyInformationalVersion("6.0.35-restructure-cakefile-and-build")]
[assembly: AssemblyGitBranch("refs/heads/restructure-cakefile-and-build")]
[assembly: AssemblyNuGetVersion("6.0.35-restructure-cakefile-and-build")]

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