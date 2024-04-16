using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class RequiredToolDownloader
{
    readonly TemporaryDirectory temporaryDirectory;
    readonly KindDownloader kindDownloader;
    readonly HelmDownloader helmDownloader;
    readonly KubeCtlDownloader kubeCtlDownloader;

    public RequiredToolDownloader(TemporaryDirectory temporaryDirectory, ILogger logger)
    {
        this.temporaryDirectory = temporaryDirectory;

        kindDownloader = new KindDownloader(logger);
        helmDownloader = new HelmDownloader(logger);
        kubeCtlDownloader = new KubeCtlDownloader(logger);
    }

    public async Task<(string KindExePath, string HelmExePath, string KubeCtlPath)> DownloadRequiredTools(CancellationToken cancellationToken)
    {
        var kindExePathTask = kindDownloader.Download(temporaryDirectory.DirectoryPath, cancellationToken);
        var helmExePathTask = helmDownloader.Download(temporaryDirectory.DirectoryPath, cancellationToken);
        var kubeCtlExePathTask = kubeCtlDownloader.Download(temporaryDirectory.DirectoryPath, cancellationToken);

        await Task.WhenAll(kindExePathTask, helmExePathTask, kubeCtlExePathTask);
        
        return (kindExePathTask.Result, helmExePathTask.Result, kubeCtlExePathTask.Result);
    }
}