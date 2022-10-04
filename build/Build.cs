// ReSharper disable RedundantUsingDirective
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode

    [Solution(GenerateProjects = true)] readonly Solution Solution = null!;

    [Parameter("Branch name for OctoVersion to use to calculate the version number. Can be set via the environment variable OCTOVERSION_CurrentBranch.",
        Name = "OCTOVERSION_CurrentBranch")]
    readonly string BranchName = null!;

    [Parameter("Whether to auto-detect the branch name - this is okay for a local build, but should not be used under CI.")]
    readonly bool AutoDetectBranch = IsLocalBuild;

    [OctoVersion(UpdateBuildNumber = true, BranchParameter = nameof(BranchName),
        AutoDetectBranchParameter = nameof(AutoDetectBranch), Framework = "net6.0")]
    readonly OctoVersionInfo OctoVersionInfo = null!;

    [Parameter] string TestFramework = "";
    [Parameter] string TestRuntime = "";

    [PackageExecutable(
        packageId: "azuresigntool",
        packageExecutable: "azuresigntool.dll")]
    public static Tool AzureSignTool = null!;

    [PackageExecutable(
        packageId: "wix",
        packageExecutable: "heat.exe")]
    readonly Tool WiXHeatTool = null!;

    [PackageExecutable(
        packageId: "OctopusTools",
        packageExecutable: "octo.exe")]
    readonly Tool OctoCliTool = null!;

    [Parameter] public static string AzureKeyVaultUrl = "";
    [Parameter] public static string AzureKeyVaultAppId = "";
    [Parameter] public static string AzureKeyVaultTenantId = "";
    [Secret] [Parameter] public static string AzureKeyVaultAppSecret = "";
    [Parameter] public static string AzureKeyVaultCertificateName = "";

    [Parameter(Name = "signing_certificate_path")] public static string SigningCertificatePath = RootDirectory / "certificates" / "OctopusDevelopment.pfx";
    [Secret] [Parameter(Name = "signing_certificate_password")] public static string SigningCertificatePassword = "Password01!";

    readonly AbsolutePath SourceDirectory = RootDirectory / "source";
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "_artifacts";
    readonly AbsolutePath BuildDirectory = RootDirectory / "_build";
    readonly AbsolutePath LocalPackagesDirectory = RootDirectory / ".." / "LocalPackages";
    readonly AbsolutePath TestDirectory = RootDirectory / "_test";

    const string NetFramework = "net48";
    const string NetCore = "netcoreapp3.1";
    readonly string[] RuntimeIds = { "win", "win-x86", "win-x64", "linux-x64", "linux-musl-x64", "linux-arm64", "linux-arm", "osx-x64" };

    [PublicAPI]
    Target CalculateVersion => _ => _
        .Executes(() =>
        {
            // This is here just so that TeamCity has a target to call. The OctoVersion attribute generates the version for us
        });

    [PublicAPI]
    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(BuildDirectory);
        });

    [PublicAPI]
    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });

    [PublicAPI]
    Target BuildWindows => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();
            using var productWxsFile = UpdateMsiProductVersion();

            RuntimeIds.Where(x => x.StartsWith("win"))
                .ForEach(runtimeId => RunBuildFor(runtimeId.Equals("win") ? NetFramework : NetCore, runtimeId));
            
            versionInfoFile.Dispose();
            productWxsFile.Dispose();
            
            var winFolder = (BuildDirectory / "Tentacle" / NetFramework / "win");
            var hardenInstallationDirectoryScript = RootDirectory / "scripts" / "Harden-InstallationDirectory.ps1";
            CopyFileToDirectory(hardenInstallationDirectoryScript, winFolder, FileExistsPolicy.Overwrite);

            // Sign any unsigned libraries that Octopus Deploy authors so that they play nicely with security scanning tools.
            // Refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
            // Decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
            var windowsOnlyBuiltFileSpec = BuildDirectory.GlobDirectories("**/win*/**");

            var filesToSign = windowsOnlyBuiltFileSpec
                .SelectMany(x => x.GlobFiles("**/Octo*.exe", "**/Octo*.dll", "**/Tentacle.exe", "**/Tentacle.dll", "**/Halibut.dll", "**/Nuget.*.dll", "**/Nevermore.dll", "**/*.ps1"))
                .Where(file => !Signing.HasAuthenticodeSignature(file))
                .ToArray();

            Signing.Sign(filesToSign);
        });

    [PublicAPI]
    Target BuildLinux => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();
            using var productWxsFile = UpdateMsiProductVersion();

            RuntimeIds.Where(x => x.StartsWith("linux-"))
                .ForEach(runtimeId => RunBuildFor(NetCore, runtimeId));
            
            versionInfoFile.Dispose();
            productWxsFile.Dispose();
        });

    [PublicAPI]
    Target BuildOsx => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();
            using var productWxsFile = UpdateMsiProductVersion();

            RuntimeIds.Where(x => x.StartsWith("osx-"))
                .ForEach(runtimeId => RunBuildFor(NetCore, runtimeId));
            
            versionInfoFile.Dispose();
            productWxsFile.Dispose();
        });

    [PublicAPI]
    Target BuildAll => _ => _
        .Description("Build all the framework/runtime combinations. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(BuildWindows)
        .DependsOn(BuildLinux)
        .DependsOn(BuildOsx);

    [PublicAPI]
    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(PackCrossPlatformBundle)
        .Description("If not running on a build agent, this step copies the relevant built artifacts to the local packages cache.")
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDirectory);
            CopyFileToDirectory(ArtifactsDirectory / "Chocolatey" / $"OctopusDeploy.Tentacle.{OctoVersionInfo.NuGetVersion}.nupkg", LocalPackagesDirectory);
        });

    [PublicAPI]
    Target Default => _ => _
        .DependsOn(Pack)
        .DependsOn(CopyToLocalPackages);

    //Modifies VersionInfo.cs to embed version information into the shipped product.
    ModifiableFileWithRestoreContentsOnDispose ModifyTemplatedVersionAndProductFilesWithValues()
    {
        var versionInfoFilePath = SourceDirectory / "Solution Items" / "VersionInfo.cs";

        var versionInfoFile = new ModifiableFileWithRestoreContentsOnDispose(versionInfoFilePath);

        versionInfoFile.ReplaceRegexInFiles("AssemblyVersion\\(\".*?\"\\)", $"AssemblyVersion(\"{OctoVersionInfo.MajorMinorPatch}\")");
        versionInfoFile.ReplaceRegexInFiles("AssemblyFileVersion\\(\".*?\"\\)", $"AssemblyFileVersion(\"{OctoVersionInfo.MajorMinorPatch}\")");
        versionInfoFile.ReplaceRegexInFiles("AssemblyInformationalVersion\\(\".*?\"\\)", $"AssemblyInformationalVersion(\"{OctoVersionInfo.InformationalVersion}\")");
        versionInfoFile.ReplaceRegexInFiles("AssemblyGitBranch\\(\".*?\"\\)", $"AssemblyGitBranch(\"{Git.DeriveGitBranch()}\")");
        versionInfoFile.ReplaceRegexInFiles("AssemblyNuGetVersion\\(\".*?\"\\)", $"AssemblyNuGetVersion(\"{OctoVersionInfo.FullSemVer}\")");

        return versionInfoFile;
    }

    //Modifies Product.wxs to embed version information into the shipped product.
    ModifiableFileWithRestoreContentsOnDispose UpdateMsiProductVersion()
    {
        var productWxsFilePath = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Product.wxs";
        
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(productWxsFilePath);

        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        namespaceManager.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");

        var product = xmlDoc.SelectSingleNode("//wi:Product", namespaceManager);

        if (product == null) throw new Exception("Couldn't find Product Node in wxs file");
        if (product.Attributes == null) throw new Exception("Couldn't find Attributes in Product Node");
        if (product.Attributes["Version"] == null) throw new Exception("Couldn't find Version attribute in Product Node");

        product.Attributes["Version"]!.Value = OctoVersionInfo.MajorMinorPatch;

        xmlDoc.Save(productWxsFilePath);

        return new ModifiableFileWithRestoreContentsOnDispose(productWxsFilePath);
    }

    void RunBuildFor(string framework, string runtimeId)
    {
        var configuration = $"Release-{framework}-{runtimeId}";
        
        DotNetPublish(p => p
            .SetProject(SourceDirectory / "Tentacle.sln")
            .SetConfiguration(configuration)
            .SetFramework(framework)
            .SetRuntime(runtimeId)
            .EnableNoRestore()
            .SetVersion(OctoVersionInfo.FullSemVer));
    }

    // We need to use tar directly, because .NET utilities aren't able to preserve the file permissions
    // Importantly, the Tentacle executable needs to be +x in the tar.gz file
    void TarGZipCompress(AbsolutePath inputDirectory, string fileSpec, AbsolutePath outputDirectory, string outputFile)
    {
        DockerTasks.DockerRun(settings => settings
            .EnableRm()
            .EnableTty()
            .SetVolume($"{inputDirectory}:/input", $"{outputDirectory}:/output")
            .SetCommand("debian")
            .SetArgs("tar", "-C", "/input", "-czvf", $"/output/{outputFile}", fileSpec, "--preserve-permissions"));
    }

    public static int Main () => Execute<Build>(x => x.Default);
}