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
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using Nuke.OctoVersion;
using OctoVersion.Core;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.BuildWindows);

    [Solution] readonly Solution Solution = null!;
    [NukeOctoVersion] readonly OctoVersionInfo OctoVersionInfo = null!;

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
    [Secret] public static string AzureKeyVaultAppSecret = "";
    [Parameter] public static string AzureKeyVaultCertificateName = "";
    
    [Parameter(Name = "signing_certificate_path")] public static string SigningCertificatePath = RootDirectory / "certificates" / "OctopusDevelopment.pfx";
    [Parameter(Name = "signing_certificate_password")] public static string SigningCertificatePassword = "Password01!";

    readonly AbsolutePath SourceDirectory = RootDirectory / "source";
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "_artifacts";
    readonly AbsolutePath BuildDirectory = RootDirectory / "_build";
    readonly AbsolutePath LocalPackagesDirectory = RootDirectory / ".." / "LocalPackages";
    readonly AbsolutePath TestDirectory = RootDirectory / "_test";
    
    const string NetFramework = "net452";
    const string NetCore = "netcoreapp3.1";
    readonly string[] RuntimeIds = { "win", "win-x86", "win-x64", "linux-x64", "linux-musl-x64", "linux-arm64", "linux-arm", "osx-x64" };

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
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    [PublicAPI]
    Target BuildWindows => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            static bool HasAuthenticodeSignature(AbsolutePath fileInfo)
            {
                // note: Doesn't check if existing signatures are valid, only that one exists
                // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
                try
                {
                    X509Certificate.CreateFromSignedFile(fileInfo);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            ModifyTemplatedVersionAndProductFilesWithValues(out var versionInfoRestoreAction, out var productWxsRestoreAction);

            RuntimeIds.Where(x => x.StartsWith("win"))
                .ForEach(runtimeId => RunBuildFor(runtimeId.Equals("win") ? NetFramework : NetCore, runtimeId));

            versionInfoRestoreAction.Invoke();
            productWxsRestoreAction.Invoke();
            
            // Sign any unsigned libraries that Octopus Deploy authors so that they play nicely with security scanning tools.
            // Refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
            // Decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
            var windowsOnlyBuiltFileSpec = BuildDirectory.GlobDirectories($"**/win*/**");
        
            var filesToSign = windowsOnlyBuiltFileSpec
                .SelectMany(x => x.GlobFiles("**/Octo*.exe", "**/Octo*.dll", "**/Tentacle.exe", "**/Tentacle.dll", "**/Halibut.dll", "**/Nuget.*.dll"))
                .Where(file => !HasAuthenticodeSignature(file))
                .ToArray();

            Signing.Sign(filesToSign);
        });

    [PublicAPI]
    Target BuildLinux => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            ModifyTemplatedVersionAndProductFilesWithValues(out var versionInfoRestoreAction, out var productWxsRestoreAction);

            RuntimeIds.Where(x => x.StartsWith("linux-"))
                .ForEach(runtimeId => RunBuildFor(NetCore, runtimeId));
            
            versionInfoRestoreAction.Invoke();
            productWxsRestoreAction.Invoke();
        });

    [PublicAPI]
    Target BuildOsx => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            ModifyTemplatedVersionAndProductFilesWithValues(out var versionInfoRestoreAction, out var productWxsRestoreAction);

            RuntimeIds.Where(x => x.StartsWith("osx-"))
                .ForEach(runtimeId => RunBuildFor(NetCore, runtimeId));

            versionInfoRestoreAction.Invoke();
            productWxsRestoreAction.Invoke();
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

    void RunTests()
    {
        Logger.Info($"Running test for Framework: {TestFramework} and Runtime: {TestRuntime}");

        EnsureExistingDirectory(ArtifactsDirectory / "teamcity");
            
        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var octopusTentacleTestsDirectory = BuildDirectory / "Octopus.Tentacle.Tests" / TestFramework / TestRuntime;
        var testAssembliesPath = octopusTentacleTestsDirectory.GlobFiles("*.Tests.dll");
        var testResultsPath = ArtifactsDirectory / "teamcity" / $"TestResults-{TestFramework}-{TestRuntime}.xml";
        
        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            testAssembliesPath.ForEach(projectPath =>
                DotNetTest(settings => settings
                    .SetProjectFile(projectPath)
                    .SetFramework(TestFramework)
                    .SetLoggers($"trx;LogFileName={testResultsPath}"))
                );
        }
        catch (Exception e)
        {
            Logger.Warn($"{e.Message}: {e}");
        }
    }

    //Modifies the VersionInfo.cs and Product.wxs files to embed version information into the shipped product.
    void ModifyTemplatedVersionAndProductFilesWithValues(out Action versionInfoRestoreAction, out Action productWxsRestoreAction)
    {
        void UpdateMsiProductVersion(AbsolutePath productWxs)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(productWxs);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");

            var product = xmlDoc.SelectSingleNode("//wi:Product", namespaceManager);

            if (product == null) throw new Exception("Couldn't find Product Node in wxs file");
            if (product.Attributes == null) throw new Exception("Couldn't find Version attribute in Product Node");

            // ReSharper disable once PossibleNullReferenceException
            product.Attributes["Version"]!.Value = OctoVersionInfo.MajorMinorPatch;

            xmlDoc.Save(productWxs);
        }

        static void ReplaceRegexInFiles(AbsolutePath file, string matchingPattern, string replacement)
        {
            var fileText = File.ReadAllText(file);
            fileText = Regex.Replace(fileText, matchingPattern, replacement);
            File.WriteAllText(file, fileText);
        }

        var versionInfoFile = SourceDirectory / "Solution Items" / "VersionInfo.cs";
        var productWxs = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Product.wxs";

        //TODO: Use this action
        versionInfoRestoreAction = RestoreFileForCleanup(versionInfoFile);
        productWxsRestoreAction = RestoreFileForCleanup(productWxs);

        ReplaceRegexInFiles(versionInfoFile, "AssemblyVersion\\(\".*?\"\\)", $"AssemblyVersion(\"{OctoVersionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyFileVersion\\(\".*?\"\\)", $"AssemblyFileVersion(\"{OctoVersionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyInformationalVersion\\(\".*?\"\\)", $"AssemblyInformationalVersion(\"{OctoVersionInfo.FullSemVer}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyGitBranch\\(\".*?\"\\)", $"AssemblyGitBranch(\"{Git.DeriveGitBranch()}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyNuGetVersion\\(\".*?\"\\)", $"AssemblyNuGetVersion(\"{OctoVersionInfo.FullSemVer}\")");

        UpdateMsiProductVersion(productWxs);
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
    
    static void ReplaceTextInFiles(AbsolutePath path, string oldValue, string newValue)
    {
        var fileText = File.ReadAllText(path);
        fileText = fileText.Replace(oldValue, newValue);
        File.WriteAllText(path, fileText);
    }
    
    Action RestoreFileForCleanup(AbsolutePath file)
    {
        var contents = File.ReadAllBytes(file);
        return () => {
            Logger.Info("Restoring {0}", file);
            File.WriteAllBytes(file, contents);
        };
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
}
