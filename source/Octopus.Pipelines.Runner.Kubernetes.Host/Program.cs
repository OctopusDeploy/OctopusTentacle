using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Kubernetes.Host;
using Octopus.Pipelines.Runner.Gateway;

var builder = Host.CreateApplicationBuilder(args);
RunnerHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();
var runner = host.Services.GetRequiredService<IOptions<GatewayOptions>>().Value;
host.Services.GetRequiredService<ILogger<Program>>().LogInformation(
    "Runner {RunnerId} starting in cell {CellId} with backend {Backend}, pulling up to {Capacity} jobs at once ({StartingCap} starting), server {Server}",
    runner.RunnerId, runner.CellId, builder.Configuration.GetSection(RunnerOptions.Section).GetValue(nameof(RunnerOptions.Backend), JobBackendKind.Kubernetes),
    runner.MaxConcurrentJobs,
    runner.AdaptiveStarts.Enabled ? $"adaptive, up to {(runner.MaxStartingJobs > 0 ? runner.MaxStartingJobs : runner.MaxConcurrentJobs)}"
        : runner.MaxStartingJobs > 0 ? runner.MaxStartingJobs.ToString() : "no cap",
    runner.ServerAddress);

host.Run();
