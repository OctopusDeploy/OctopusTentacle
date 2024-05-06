using System;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
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
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1With(DecorateBeforeKubernetesStartScriptV1(beforeStartScriptDecorator));
            }

            foreach (var afterStartScriptDecorator in afterStartScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateAfterStartScript(afterStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateAfterStartScriptV2(afterStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateAfterKubernetesScriptServiceV1Alpha(afterStartScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1With(DecorateAfterKubernetesScriptServiceV1(afterStartScriptDecorator));
            }

            foreach (var beforeCancelScriptDecorator in beforeCancelScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeCancelScript(beforeCancelScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeCancelScriptV2(beforeCancelScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeCancelScriptV1Alpha(beforeCancelScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1With(DecorateBeforeCancelScriptV1(beforeCancelScriptDecorator));
            }

            foreach (var beforeGetStatusDecorator in beforeGetStatusDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeGetStatus(beforeGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeGetStatusV2(beforeGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeGetStatusV1Alpha(beforeGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1With(DecorateBeforeGetStatusV1(beforeGetStatusDecorator));
            }

            foreach (var afterGetStatusDecorator in afterGetStatusDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateAfterGetStatus(afterGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateAfterGetStatusV2(afterGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateAfterGetStatusV1Alpha(afterGetStatusDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1With(DecorateAfterGetStatusV1(afterGetStatusDecorator));
            }

            foreach (var beforeCompleteScriptDecorator in beforeCompleteScriptDecorators)
            {
                tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(DecorateBeforeCompleteScript(beforeCompleteScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(DecorateBeforeCompleteScriptV2(beforeCompleteScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1AlphaWith(DecorateBeforeCompleteScriptV1Alpha(beforeCompleteScriptDecorator));
                tentacleServiceDecoratorBuilder.DecorateKubernetesScriptServiceV1With(DecorateBeforeCompleteScriptV1(beforeCompleteScriptDecorator));
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
        
        Decorator<IAsyncClientKubernetesScriptServiceV1> DecorateBeforeKubernetesStartScriptV1(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1DecoratorBuilder();
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

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateAfterKubernetesScriptServiceV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.AfterStartScript(task);
            return builder.Build();
        }
        
        Decorator<IAsyncClientKubernetesScriptServiceV1> DecorateAfterKubernetesScriptServiceV1(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1DecoratorBuilder();
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

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeCancelScriptV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeCancelScript(task);
            return builder.Build();
        }
        
        Decorator<IAsyncClientKubernetesScriptServiceV1> DecorateBeforeCancelScriptV1(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1DecoratorBuilder();
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

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeGetStatusV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeGetStatus(task);
            return builder.Build();
        }

        Decorator<IAsyncClientKubernetesScriptServiceV1> DecorateBeforeGetStatusV1(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1DecoratorBuilder();
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

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateAfterGetStatusV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.AfterGetStatus(task);
            return builder.Build();
        }
        
        Decorator<IAsyncClientKubernetesScriptServiceV1> DecorateAfterGetStatusV1(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1DecoratorBuilder();
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

        Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> DecorateBeforeCompleteScriptV1Alpha(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1AlphaDecoratorBuilder();
            builder.BeforeCompleteScript(task);
            return builder.Build();
        }
        
        Decorator<IAsyncClientKubernetesScriptServiceV1> DecorateBeforeCompleteScriptV1(Func<Task> task)
        {
            var builder = new KubernetesScriptServiceV1DecoratorBuilder();
            builder.BeforeCompleteScript(task);
            return builder.Build();
        }
    }
}