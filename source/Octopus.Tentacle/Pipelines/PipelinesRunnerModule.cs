#if PIPELINES_RUNNER
using Autofac;
using Octopus.Tentacle.Background;

namespace Octopus.Tentacle.Pipelines
{
    public class PipelinesRunnerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<RunnerBackgroundTask>().As<IBackgroundTask>().SingleInstance();
        }
    }
}
#endif
