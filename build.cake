//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0007"
#tool "nuget:?package=WiX&version=3.10.3"

using Path = System.IO.Path;
using Dir = System.IO.Directory;
using System.Xml;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var verbosity = Argument<Verbosity>("verbosity", Verbosity.Quiet);

var signingCertificatePath = Argument("signing_certicifate_path", "./certificates/OctopusDevelopment.pfx");
var signingCertificatPassword = Argument("signing_certificate_password", "Password01!");

// Keep this list in order by most likely to succeed
var signingTimestampUrls = new string[] {
    "http://timestamp.globalsign.com/scripts/timestamp.dll",
    "http://www.startssl.com/timestamp",
    "http://timestamp.comodoca.com/rfc3161", 
    "http://timestamp.verisign.com/scripts/timstamp.dll",
    "http://tsa.starfieldtech.com"};

var packageDir = "./build/package";
var installerDir = "./build/installer";
var artifactsDir = "./build/artifacts";
var localPackagesDir = "../LocalPackages";

GitVersion gitVersion;

var cleanups = new List<Action>();

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Teardown(context =>
{
    Information("Cleaning up");
    foreach(var cleanup in cleanups)
        cleanup();

    Information("Finished running tasks for build v{0}", gitVersion.NuGetVersion);
});


//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////
Task("__Default")
    .IsDependentOn("__Version")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__Build")
    .IsDependentOn("__SignBuiltFiles")
    .IsDependentOn("__CreateTentacleInstaller")
    .IsDependentOn("__PackNuget")
    .IsDependentOn("__CopyToLocalPackages");

Task("__Version")
    .IsDependentOn("__GitVersionAssemblies")
    .Does(() =>
{
    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(gitVersion.NuGetVersion);

    Information("Building OctopusClients v{0}", gitVersion.NuGetVersion);
});

Task("__GitVersionAssemblies")
    .Does(() =>
{
    var gitVersionFile = "./source/Solution Items/VersionInfo.cs";

    RestoreFileOnCleanup(gitVersionFile);

    gitVersion = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        UpdateAssemblyInfoFilePath = gitVersionFile
    });
});

Task("__Clean")
    .Does(() =>
{
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectory(packageDir);
    CleanDirectory(installerDir);
    CleanDirectory(artifactsDir);
});

Task("__Restore")
    .Does(() => NuGetRestore("./source/Tentacle.sln"));

Task("__Build")
    .Does(() =>
{
    MSBuild("./source/Tentacle.sln", settings =>
        settings
            .SetConfiguration(configuration)
            .SetVerbosity(verbosity)
    );
});

Task("__SignBuiltFiles")
    .Does(() =>
{
    var filesToSign = new string[] {
        "./source/Octopus.Tentacle/bin/Octopus*.dll",
        "./source/Octopus.Tentacle/bin/Octopus*.exe",
        "./source/Octopus.Tentacle/bin/Tentacle.exe",
        "./source/Octopus.Manager.Tentacle/bin/Octopus*.dll",
        "./source/Octopus.Manager.Tentacle/bin/Octopus*.exe"
    };

    SignAndTimestamp(filesToSign);
});


Task("__CreateTentacleInstaller")
    .IsDependentOn("__UpdateWixVersion")
    .Does(() =>
{
    CreateDirectory(installerDir);
    CopyFiles("./source/Octopus.Manager.Tentacle/bin/*", installerDir);
    CopyFiles("./source/Octopus.Tentacle/bin/*", installerDir);

    CleanBinariesDirectory(installerDir);

    Information("Generating installer contents");
    var harvestDirectory = Directory(installerDir);

    var harvestFile = "./source/Octopus.Tentacle.Installer/Tentacle.Generated.wxs";
    RestoreFileOnCleanup(harvestFile);

    var heatSettings = new HeatSettings {
        NoLogo = true,
        GenerateGuid = true,
        SuppressFragments = true,
        SuppressRootDirectory = true,
        SuppressRegistry = true,
        SuppressUniqueIds = true,
        ComponentGroupName = "TentacleComponents",
        PreprocessorVariable = "var.TentacleSource",
        DirectoryReferenceId = "INSTALLLOCATION"
    };

    WiXHeat(harvestDirectory, File(harvestFile), WiXHarvestType.Dir, heatSettings);

    var allowUpgrade = !string.Equals(gitVersion.PreReleaseLabel, "alpha");

    Information("Building 32 bit installer");

    MSBuild("./source/Octopus.Tentacle.Installer/Octopus.Tentacle.Installer.wixproj", settings =>
        settings
            .SetConfiguration(configuration)
            .WithProperty("AllowUpgrade", allowUpgrade.ToString())
            .SetVerbosity(verbosity)
            .WithTarget("build")
    );


});

Task("__UpdateWixVersion")
    .Does(() =>
{
    var installerProductFile = "./source/Octopus.Tentacle.Installer/Product.wxs";
    RestoreFileOnCleanup(installerProductFile);

    var xmlDoc = new XmlDocument();
    xmlDoc.Load(installerProductFile);

    var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
    nsmgr.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");

    var product = xmlDoc.SelectSingleNode("//wi:Product", nsmgr);
    product.Attributes["Version"].Value = gitVersion.MajorMinorPatch;
    xmlDoc.Save(installerProductFile);
});

Task("__PackNuget")
    .Does(() =>
{
    CreateDirectory(packageDir);
    CopyFiles("./source/Octopus.Manager.Tentacle/bin/*", packageDir);
    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.nuspec", packageDir);

    CreateDirectory(artifactsDir);
    NuGetPack(Path.Combine(packageDir, "Tentacle.nuspec"), new NuGetPackSettings {
        Version = gitVersion.NuGetVersion,
        OutputDirectory = artifactsDir
    });
});

Task("__CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("__PackNuget")
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Tentacle.{gitVersion.NuGetVersion}.nupkg"), localPackagesDir);
});


private void RestoreFileOnCleanup(string file)
{
    var contents = System.IO.File.ReadAllBytes(file);
    cleanups.Add(() => {
        Information("Restoring {0}", file);
        System.IO.File.WriteAllBytes(file, contents);
    });
}


private void CleanBinariesDirectory(string directory)
{
    Information($"Cleaning {directory}");
    DeleteFiles($"{directory}/*.xml");
}

private void SignAndTimestamp(params string[] paths)
{
    var allFiles = new List<FilePath>();
    foreach (var path in paths)
    {
        var files = GetFiles(path);
        allFiles.AddRange(files);
    }
    SignAndTimestamp(allFiles.ToArray());
}

private void SignAndTimestamp(params FilePath[] assemblies)
{
    var lastException = default(Exception);
    var signSettings = new SignToolSignSettings
    {
        CertPath = File(signingCertificatePath),
        Password = signingCertificatPassword
    };
    foreach (var url in signingTimestampUrls)
    {
        Information($"  Trying to time stamp {assemblies} using {url}");
        signSettings.TimeStampUri = new Uri(url);
        try
        {
            Sign(assemblies, signSettings);
            return;
        }
        catch (Exception ex)
        {
            lastException = ex;
        }
    }
    throw(lastException);
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