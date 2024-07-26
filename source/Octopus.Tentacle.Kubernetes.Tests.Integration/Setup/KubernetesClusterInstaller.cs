using System.Diagnostics;
using System.Reflection;
using System.Text;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Logging;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Support;
using Octopus.Tentacle.Util;
using PlatformDetection = Octopus.Tentacle.CommonTestUtils.PlatformDetection;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesClusterInstaller
{
    readonly string clusterName;
    readonly string kubeConfigName;

    readonly TemporaryDirectory tempDir;
    readonly string kindExePath;
    readonly string helmExePath;
    readonly string kubeCtlPath;
    readonly ILogger logger;

    public string KubeConfigPath => Path.Combine(tempDir.DirectoryPath, kubeConfigName);
    public string ClusterName => clusterName;

    public KubernetesClusterInstaller(TemporaryDirectory tempDirectory, string kindExePath, string helmExePath, string kubeCtlPath, ILogger logger)
    {
        tempDir = tempDirectory;
        this.kindExePath = kindExePath;
        this.helmExePath = helmExePath;
        this.kubeCtlPath = kubeCtlPath;
        this.logger = logger;

        clusterName = $"tentaclient-{DateTime.Now:yyyyMMddhhmmss}";
        kubeConfigName = $"{clusterName}.config";
    }

    public async Task Install()
    {
        var configFilePath = await WriteFileToTemporaryDirectory("kind-config.yaml");

        var sw = new Stopwatch();
        sw.Restart();
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kindExePath,
            //we give the cluster a unique name
            $"create cluster --name={clusterName} --config=\"{configFilePath}\" --kubeconfig=\"{kubeConfigName}\"",
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);

        sw.Stop();

        if (exitCode != 0)
        {
            logger.Error("Failed to create Kind Kubernetes cluster {ClusterName}", clusterName);
            throw new InvalidOperationException($"Failed to create Kind Kubernetes cluster {clusterName}");
        }

        logger.Information("Test cluster kubeconfig path: {Path:l}", KubeConfigPath);

        logger.Information("Created Kind Kubernetes cluster {ClusterName} in {ElapsedTime}", clusterName, sw.Elapsed);

        await SetLocalhostRouting();

        await InstallNfsCsiDriver();
    }

    async Task SetLocalhostRouting()
    {
        var filename = PlatformDetection.IsRunningOnNix ? "linux-network-routing.yaml" : "docker-desktop-network-routing.yaml";

        var manifestFilePath = await WriteFileToTemporaryDirectory(filename, "manifest.yaml");

        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .CreateLogger();

        var exitCode = SilentProcessRunner.ExecuteCommand(
            kubeCtlPath,
            //we give the cluster a unique name
            $"apply -n default -f \"{manifestFilePath}\" --kubeconfig=\"{KubeConfigPath}\"",
            tempDir.DirectoryPath,
            sprLogger.Debug,
            sprLogger.Information,
            sprLogger.Error,
            CancellationToken.None);

        if (exitCode != 0)
        {
            logger.Error("Failed to apply localhost routing to cluster {ClusterName}", clusterName);
            throw new InvalidOperationException($"Failed to apply localhost routing to cluster {clusterName}. Logs: {sb}");
        }
    }

    async Task<string> WriteFileToTemporaryDirectory(string resourceFileName, string? outputFilename = null)
    {
        await using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStreamFromPartialName(resourceFileName);

        var filePath = Path.Combine(tempDir.DirectoryPath, outputFilename ?? resourceFileName);
        await using var file = File.Create(filePath);

        resourceStream.Seek(0, SeekOrigin.Begin);
        await resourceStream.CopyToAsync(file);

        return filePath;
    }

    async Task InstallNfsCsiDriver()
    {
        //we need to perform a repo update in helm first
        // var exitCode = SilentProcessRunner.ExecuteCommand(
        //     helmPath,
        //     "repo update",
        //     tempDir.DirectoryPath,
        //     logger.Debug,
        //     logger.Information,
        //     logger.Error,
        //     CancellationToken.None);

        var installArgs = BuildNfsCsiDriverInstallArguments();
        
        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .CreateLogger();
        
        var exitCode = SilentProcessRunner.ExecuteCommand(
            helmExePath,
            installArgs,
            tempDir.DirectoryPath,
            sprLogger.Debug,
            sprLogger.Information,
            sprLogger.Error,
            CancellationToken.None);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to install NFS CSI driver into cluster {clusterName}. Logs: {sb}");
        }
    }

    string BuildNfsCsiDriverInstallArguments()
    {
        return string.Join(" ",
            "install",
            "--atomic",
            "--repo https://raw.githubusercontent.com/kubernetes-csi/csi-driver-nfs/master/charts",
            "--namespace kube-system",
            "--version v4.6.0",
            $"--kubeconfig \"{KubeConfigPath}\"",
            "csi-driver-nfs",
            "csi-driver-nfs"
        );
    }

    public void Dispose()
    {
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kindExePath,
            //delete the cluster for this test run
            $"delete cluster --name={clusterName}",
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);

        if (exitCode != 0)
        {
            logger.Error("Failed to delete Kind kubernetes cluster {ClusterName}", clusterName);
        }
    }
}
