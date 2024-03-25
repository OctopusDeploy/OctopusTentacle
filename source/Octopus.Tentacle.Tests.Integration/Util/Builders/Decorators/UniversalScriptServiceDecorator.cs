using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class UniversalScriptServiceDecorator
    {
        readonly List<Func<Task>> beforeStartScriptDecorators;
        readonly List<Func<Task>> afterStartScriptDecorators;
        readonly List<Func<Task>> beforeCancelScriptDecorators;
        readonly List<Func<Task>> beforeGetStatusDecorators;
        readonly List<Func<Task>> afterGetStatusDecorators;
        readonly List<Func<Task>> beforeCompleteScriptDecorators;

        public UniversalScriptServiceDecorator(
            List<Func<Task>> beforeStartScriptDecorators,
            List<Func<Task>> afterStartScriptDecorators,
            List<Func<Task>> beforeCancelScriptDecorators,
            List<Func<Task>> beforeGetStatusDecorators,
            List<Func<Task>> afterGetStatusDecorators,
            List<Func<Task>> beforeCompleteScriptDecorators
        )
        {
            this.beforeStartScriptDecorators = beforeStartScriptDecorators;
            this.afterStartScriptDecorators = afterStartScriptDecorators;
            this.beforeCancelScriptDecorators = beforeCancelScriptDecorators;
            this.beforeGetStatusDecorators = beforeGetStatusDecorators;
            this.afterGetStatusDecorators = afterGetStatusDecorators;
            this.beforeCompleteScriptDecorators = beforeCompleteScriptDecorators;
        }

        public void Decorate(TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder)
        {
            foreach (var beforeStartScriptDecorator in beforeStartScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeStartScript(beforeStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeStartScriptV2(beforeStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeKubernetesStartScriptV1Alpha(beforeStartScriptDecorator));
            }

            foreach (var afterStartScriptDecorator in afterStartScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateAfterStartScript(afterStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateAfterStartScriptV2(afterStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateAfterKubernetesStartScriptV1Alpha(afterStartScriptDecorator));
            }

            foreach (var beforeCancelScriptDecorator in beforeCancelScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeCancelScript(beforeCancelScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeCancelScriptV2(beforeCancelScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeKubernetesCancelScriptV1Alpha(beforeCancelScriptDecorator));
            }

            foreach (var beforeGetStatusDecorator in beforeGetStatusDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeGetStatus(beforeGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeGetStatusV2(beforeGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeKubernetesGetStatusV1Alpha(beforeGetStatusDecorator));
            }

            foreach (var afterGetStatusDecorator in afterGetStatusDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateAfterGetStatus(afterGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateAfterGetStatusV2(afterGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateAfterKubernetesGetStatusV1Alpha(afterGetStatusDecorator));
            }

            foreach (var beforeCompleteScriptDecorator in beforeCompleteScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeCompleteScript(beforeCompleteScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeCompleteScriptV2(beforeCompleteScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeKubernetesCompleteScriptV1Alpha(beforeCompleteScriptDecorator));
            }
        }

        Decorator<IAsyncClientScriptService> DecorateBeforeStartScript(Func<Task> task)
        {
            var builder = new ScriptServiceDecoratorBuilder();
            builder.BeforeStartScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptServiceV2> DecorateBeforeStartScriptV2(Func<Task> task)
        {
            var builder = new ScriptServiceV2DecoratorBuilder();
            builder.BeforeStartScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeKubernetesStartScriptV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeStartScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptService> DecorateAfterStartScript(Func<Task> task)
        {
            var builder = new ScriptServiceDecoratorBuilder();
            builder.AfterStartScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptServiceV2> DecorateAfterStartScriptV2(Func<Task> task)
        {
            var builder = new ScriptServiceV2DecoratorBuilder();
            builder.AfterStartScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateAfterKubernetesStartScriptV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.AfterStartScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptService> DecorateBeforeCancelScript(Func<Task> task)
        {
            var builder = new ScriptServiceDecoratorBuilder();
            builder.BeforeCancelScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptServiceV2> DecorateBeforeCancelScriptV2(Func<Task> task)
        {
            var builder = new ScriptServiceV2DecoratorBuilder();
            builder.BeforeCancelScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeKubernetesCancelScriptV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeCancelScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptService> DecorateBeforeGetStatus(Func<Task> task)
        {
            var builder = new ScriptServiceDecoratorBuilder();
            builder.BeforeGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptServiceV2> DecorateBeforeGetStatusV2(Func<Task> task)
        {
            var builder = new ScriptServiceV2DecoratorBuilder();
            builder.BeforeGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeKubernetesGetStatusV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptService> DecorateAfterGetStatus(Func<Task> task)
        {
            var builder = new ScriptServiceDecoratorBuilder();
            builder.AfterGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptServiceV2> DecorateAfterGetStatusV2(Func<Task> task)
        {
            var builder = new ScriptServiceV2DecoratorBuilder();
            builder.AfterGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateAfterKubernetesGetStatusV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.AfterGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptService> DecorateBeforeCompleteScript(Func<Task> task)
        {
            var builder = new ScriptServiceDecoratorBuilder();
            builder.BeforeCompleteScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientScriptServiceV2> DecorateBeforeCompleteScriptV2(Func<Task> task)
        {
            var builder = new ScriptServiceV2DecoratorBuilder();
            builder.BeforeCompleteScript(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeKubernetesCompleteScriptV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeCompleteScript(task);
            return builder.Build();
        }
    }
}