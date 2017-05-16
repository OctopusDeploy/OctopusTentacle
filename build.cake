//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0007"

using Path = System.IO.Path;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var packageDir = "./build/package";
var artifactsDir = "./build/artifacts";
var localPackagesDir = "../LocalPackages";

string nugetVersion;

var cleanups = new List<Action>();

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Teardown(context =>
{
    Information("Cleaning up");
    foreach(var cleanup in cleanups)
        cleanup();

    Information("Finished running tasks for build v{0}", nugetVersion);
});


//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////
Task("__Default")
    .IsDependentOn("__Version")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__Build")
    .IsDependentOn("__PackNuget")
    .IsDependentOn("__CopyToLocalPackages");

Task("__Version")
    .IsDependentOn("__GitVersionAssemblies")
    .Does(() =>
{
    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(nugetVersion);

    Information("Building OctopusClients v{0}", nugetVersion);
});

Task("__GitVersionAssemblies")
    .Does(() =>
{
    var versionInfoFile = "./source/Solution Items/VersionInfo.cs";

    RestoreFileOnCleanup(versionInfoFile);

    var gitVersionInfo = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        UpdateAssemblyInfoFilePath = versionInfoFile
    });

    nugetVersion = gitVersionInfo.NuGetVersion;
});

Task("__Clean")
    .Does(() =>
{
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectory(packageDir);
    CleanDirectory(artifactsDir);
});

Task("__Restore")
    .Does(() => NuGetRestore("./source/Tentacle.sln"));

Task("__Build")
    .Does(() =>
{
    MSBuild("./source/Tentacle.sln", settings =>
        settings.SetConfiguration(configuration));
});

Task("__PackNuget")
    .Does(() =>
{
    CreateDirectory(packageDir);
    CopyFiles("./source/Octopus.Manager.Tentacle/bin/*", packageDir);
    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.nuspec", packageDir);

    CreateDirectory(artifactsDir);
    NuGetPack(Path.Combine(packageDir, "Tentacle.nuspec"), new NuGetPackSettings {
        Version = nugetVersion,
        OutputDirectory = artifactsDir
    });
});

Task("__CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("__PackNuget")
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Tentacle.{nugetVersion}.nupkg"), localPackagesDir);
});


private void RestoreFileOnCleanup(string file)
{
    var contents = System.IO.File.ReadAllBytes(file);
    cleanups.Add(() => {
        Information("Restoring {0}", file);
        System.IO.File.WriteAllBytes(file, contents);
    });
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