using System.Diagnostics;
using System.Reflection;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesClusterInstaller
{
    readonly string clusterName;
    readonly string kubeConfigName;

    readonly TemporaryDirectory tempDir;
    string kindExe = null!;
    readonly ILogger logger;

    public string KubeConfigPath => Path.Combine(tempDir.DirectoryPath, kubeConfigName);

    public KubernetesClusterInstaller()
    {
        tempDir = new TemporaryDirectory();

        logger = new LoggerConfiguration()
            .WriteTo.NUnitOutput()
            .WriteTo.Console()
            .WriteTo.File("w:\\temp\\cluster-install.log")
            .CreateLogger()
            .ForContext<KubernetesClusterInstaller>();

        clusterName = $"tentacleint-{DateTime.Now:yyyyMMddhhmmss}";
        kubeConfigName = $"{clusterName}.config";
    }

    public async Task Install()
    {
        var kindDownloader = new KindDownloader(logger);
        kindExe = await kindDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);

        var configFilePath = await WriteKindConfigFile();

        var sw = new Stopwatch();
        sw.Restart();
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kindExe,
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

        logger.Information("Created Kind Kubernetes cluster {ClusterName} in {ElapsedTime}", clusterName, sw.Elapsed);

        await InstallNfiCsiDriver();
    }

    async Task<string> WriteKindConfigFile()
    {
        var asm = Assembly.GetExecutingAssembly();
        var valuesFileName = asm.GetManifestResourceNames().First(n => n.Contains("kind-config.yaml", StringComparison.OrdinalIgnoreCase));
        await using var resourceStream = asm.GetManifestResourceStream(valuesFileName)!;

        var filePath = Path.Combine(tempDir.DirectoryPath, "kind-config.yaml");
        await using var file = File.Create(filePath);

        resourceStream.Seek(0, SeekOrigin.Begin);
        await resourceStream.CopyToAsync(file);

        return filePath;
    }

    async Task InstallNfiCsiDriver()
    {
        var helmDownloader = new HelmDownloader(logger);
        var helmPath = await helmDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);

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
        var exitCode = SilentProcessRunner.ExecuteCommand(
            helmPath,
            installArgs,
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to install NFS CSI driver into cluster {clusterName}");
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
            kindExe,
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

        tempDir.Dispose();
    }
}