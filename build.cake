//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=5.3.3"

#addin "nuget:?package=Cake.FileHelpers&version=3.2.1"

using Path = System.IO.Path;
using Dir = System.IO.Directory;
using System.Xml;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var verbosity = Argument<Verbosity>("verbosity", Verbosity.Quiet);

var packageDir = "./build/package";
var artifactsDir = "./build/artifacts";
var localPackagesDir = "../LocalPackages";

GitVersion gitVersion;

var cleanups = new List<Action>();

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    CreateDirectory(packageDir);
    CreateDirectory(artifactsDir);
});

Teardown(context =>
{
    Information("Cleaning up");
    foreach(var cleanup in cleanups)
        cleanup();

    Information("Finished running tasks for build v{0}", gitVersion?.NuGetVersion);
});


//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////
Task("__Default")
    .IsDependentOn("__Version")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__PublishWindowsTestArtifact")
    .IsDependentOn("__PublishLinuxTestArtifact")
    .IsDependentOn("__CreateNuGet")
    .IsDependentOn("__CopyToLocalPackages");

Task("__Version")
    .IsDependentOn("__GitVersionAssemblies")
    .Does(() =>
{
    if(BuildSystem.IsRunningOnTeamCity)
    {
        GitVersion(new GitVersionSettings {
            OutputType = GitVersionOutput.BuildServer
        });
    }

    Information("Building OctopusShared v{0}", gitVersion.NuGetVersion);
});

Task("__GitVersionAssemblies")
    .Does(() =>
{
    try
    {
        var gitVersionFile = "./source/Solution Items/VersionInfo.cs";

        RestoreFileOnCleanup(gitVersionFile);

        Information("Getting version information and updating attributes");
        gitVersion = GitVersion(new GitVersionSettings {
            UpdateAssemblyInfo = true,
            UpdateAssemblyInfoFilePath = gitVersionFile,
            OutputType = GitVersionOutput.Json
        });

        Information("Setting BranchName and NuGetVersion");
        ReplaceRegexInFiles(gitVersionFile, "BranchName = \".*?\"", $"BranchName = \"{gitVersion.BranchName}\"");
        ReplaceRegexInFiles(gitVersionFile, "NuGetVersion = \".*?\"", $"NuGetVersion = \"{gitVersion.NuGetVersion}\"");
    }
    catch(Exception ex)
    {
        Error("Exception " + ex);
        throw;
    }
});

Task("__Clean")
    .Does(() =>
{
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectories("./source/**/TestResults");
    CleanDirectory(packageDir);
    CleanDirectory(artifactsDir);
});

Task("__Restore")
    .Does(() => DotNetCoreRestore("./source"));

Task("__PublishWindowsTestArtifact")
    .Does(() =>
{
    DotNetCorePublish("./source/Octopus.Shared.Tests/Octopus.Shared.Tests.csproj", new DotNetCorePublishSettings
    {
        Configuration = "Release",
        Framework = "net452",
        Runtime = "win-x64",
        OutputDirectory = new DirectoryPath($"{artifactsDir}/publish/win-x64")
    });
});

Task("__PublishLinuxTestArtifact")
    .Does(() =>
{
    DotNetCorePublish("./source/Octopus.Shared.Tests/Octopus.Shared.Tests.csproj", new DotNetCorePublishSettings
    {
        Configuration = "Release",
        Framework = "netcoreapp3.1",
        Runtime = "linux-x64",
        OutputDirectory = new DirectoryPath($"{artifactsDir}/publish/linux-x64")
    });
});

Task("__CreateNuGet")
    .Does(() =>
{
    DotNetCorePack("./source/Octopus.Shared/Octopus.Shared.csproj", new DotNetCorePackSettings
    {
        Configuration = "Release",
        NoRestore = true,
        NoBuild = true,
        IncludeSymbols = true,
        OutputDirectory = new DirectoryPath(artifactsDir),
        ArgumentCustomization = args => args.Append($"/p:Version={gitVersion.NuGetVersion} /p:NoWarn=NU5104")
    });
});

Task("__CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("__CreateNuGet")
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Octopus.Shared.{gitVersion.NuGetVersion}.nupkg"), localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Octopus.Shared.{gitVersion.NuGetVersion}.symbols.nupkg"), localPackagesDir);
});

private void InBlock(string block, Action action)
{
    if (TeamCity.IsRunningOnTeamCity)
        TeamCity.WriteStartBlock(block);
    else
        Information($"Starting {block}");

    try
    {
        action();
    }
    finally
    {
        if (TeamCity.IsRunningOnTeamCity)
            TeamCity.WriteEndBlock(block);
        else
            Information($"Finished {block}");
    }
}

private void RestoreFileOnCleanup(string file)
{
    var contents = System.IO.File.ReadAllBytes(file);
    cleanups.Add(() => {
        Information("Restoring {0}", file);
        try
        {
            System.IO.File.WriteAllBytes(file, contents);
        }
        catch(Exception ex)
        {
            Warning("Could not restore {0}: {1}", file, ex);
        }
    });
}

private void CleanBinariesDirectory(string directory)
{
    Information($"Cleaning {directory}");
    DeleteFiles($"{directory}/*.xml");
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("__Default");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);