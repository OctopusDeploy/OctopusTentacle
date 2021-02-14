using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });

    Target CalculateVersion => _ => _
        .Executes(() =>
        {
            GitVersion = GitVersionTasks
                .GitVersion(s => s
                    .SetTargetPath(".")
                    .SetNoFetch(true)
                    .SetFramework("netcoreapp3.0")
                )
                .Result;
        });

    Target Compile => _ => _
        .DependsOn(CalculateVersion)
        .DependsOn(Clean)
        .DependsOn(Restore)
        .Executes(() =>
        {
            Logger.Info("Building Octopus.CoreUtilities v{0}", GitVersion.FullSemVer);
            Logger.Info("Informational Version {0}", GitVersion.InformationalVersion);

            DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

/*
    Target Test => _ => _
        .DependsOn(CalculateVersion)
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetNoBuild(true)
                .EnableNoRestore());
        });
*/
    Target Pack => _ => _
        .DependsOn(CalculateVersion)
        .DependsOn(Compile)
        //.DependsOn(Test)
        .Executes(() =>
        {
            DotNetPack(_ => _
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetNoBuild(true)
                .AddProperty("Version", GitVersion.FullSemVer)
            );
        });

    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(Pack)
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDirectory);
            CopyFileToDirectory(ArtifactsDirectory / $"Octopus.CoreUtilities.{GitVersion.FullSemVer}.nupkg", LocalPackagesDirectory, FileExistsPolicy.Overwrite);
        });

    Target Default => _ => _
        .DependsOn(CopyToLocalPackages);

    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Default);
}