using System;
using System.Collections.Generic;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public delegate T Decorator<T>(T service);

    public class TentacleServiceDecoratorBuilder
    {
        readonly List<Decorator<IAsyncClientScriptService>> scriptServiceDecorator = new ();
        readonly List<Decorator<IAsyncClientScriptServiceV2>> scriptServiceV2Decorator = new ();
        readonly List<Decorator<IAsyncClientFileTransferService>> fileTransferServiceDecorator = new ();
        readonly List<Decorator<IAsyncClientCapabilitiesServiceV2>> capabilitiesServiceV2Decorator = new ();

        public TentacleServiceDecoratorBuilder DecorateScriptServiceWith(Decorator<IAsyncClientScriptService> scriptServiceDecorator)
        {
            this.scriptServiceDecorator.Add(scriptServiceDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator)
        {
            this.scriptServiceV2Decorator.Add(scriptServiceV2Decorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Action<ScriptServiceV2DecoratorBuilder> scriptServiceV2Decorator)
        {
            var b = new ScriptServiceV2DecoratorBuilder();
            scriptServiceV2Decorator(b);
            this.DecorateScriptServiceV2With(b.Build());
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateFileTransferServiceWith(Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator)
        {
            this.fileTransferServiceDecorator.Add(fileTransferServiceDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateFileTransferServiceWith(Action<FileTransferServiceDecoratorBuilder> fileTransferServiceDecorator)
        {
            var b = new FileTransferServiceDecoratorBuilder();
            fileTransferServiceDecorator(b);
            this.DecorateFileTransferServiceWith(b.Build());
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateCapabilitiesServiceV2With(Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
        {
            this.capabilitiesServiceV2Decorator.Add(capabilitiesServiceV2Decorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateCapabilitiesServiceV2With(Action<CapabilitiesServiceV2DecoratorBuilder> capabilitiesServiceDecorator)
        {
            var b = new CapabilitiesServiceV2DecoratorBuilder();
            capabilitiesServiceDecorator(b);
            this.DecorateCapabilitiesServiceV2With(b.Build());
            return this;
        }

        internal ITentacleServiceDecoratorFactory Build()
        {
            return new FooTentacleServiceDecoratorFactory(Combine(scriptServiceDecorator), Combine(scriptServiceV2Decorator), Combine(fileTransferServiceDecorator), Combine(capabilitiesServiceV2Decorator));
        }

        public static Decorator<T> Combine<T>(List<Decorator<T>> chain) where T : class
        {

            return t =>
            {
                var reverseChain = new List<Decorator<T>>(chain);
                reverseChain.Reverse();

                foreach (var func in reverseChain)
                {
                    t = func(t);
                }

                return t;
            };
        }

        class FooTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
        {
            readonly Decorator<IAsyncClientScriptService> scriptServiceDecorator;
            readonly Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator;
            readonly Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator;
            readonly Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator;

            public FooTentacleServiceDecoratorFactory(
                Decorator<IAsyncClientScriptService> scriptServiceDecorator,
                Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator,
                Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator,
                Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
            {
                this.scriptServiceDecorator = scriptServiceDecorator;
                this.scriptServiceV2Decorator = scriptServiceV2Decorator;
                this.fileTransferServiceDecorator = fileTransferServiceDecorator;
                this.capabilitiesServiceV2Decorator = capabilitiesServiceV2Decorator;
            }

            public IAsyncClientScriptService Decorate(IAsyncClientScriptService scriptService)
            {
                return scriptServiceDecorator(scriptService);
            }

            public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 scriptService)
            {
                return scriptServiceV2Decorator(scriptService);
            }

            public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service)
            {
                return fileTransferServiceDecorator(service);
            }

            public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service)
            {
                return capabilitiesServiceV2Decorator(service);
            }
        }
    }
    
    public static class TentacleServiceDecoratorBuilderExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder LogAllCalls(this TentacleServiceDecoratorBuilder builder)
        {
            return builder.LogCallsToCapabilitiesServiceV2()
                .LogCallsToScriptService()
                .LogCallsToScriptServiceV2()
                .LogCallsToFileTransferService();
        }

        public static TentacleServiceDecoratorBuilder CountAllCalls(this TentacleServiceDecoratorBuilder builder,
            out CapabilitiesServiceV2CallCounts capabilitiesServiceV2CallCounts,
            out ScriptServiceCallCounts scriptServiceCallCounts,
            out ScriptServiceV2CallCounts scriptServiceV2CallCounts,
            out FileTransferServiceCallCounts fileTransferServiceServiceCallCounts)
        {
            builder.CountCallsToCapabilitiesServiceV2(out var capabilitiesServiceCallCountsOut)
                .CountCallsToScriptService(out var scriptServiceCallCountsOut)
                .CountCallsToScriptServiceV2(out var scriptServiceV2CallCountsOut)
                .CountCallsToFileTransferService(out var fileTransferServiceCallCountsOut);

            capabilitiesServiceV2CallCounts = capabilitiesServiceCallCountsOut;
            scriptServiceCallCounts = scriptServiceCallCountsOut;
            scriptServiceV2CallCounts = scriptServiceV2CallCountsOut;
            fileTransferServiceServiceCallCounts = fileTransferServiceCallCountsOut;

            return builder;
        }

        public static TentacleServiceDecoratorBuilder LogAndCountAllCalls(this TentacleServiceDecoratorBuilder builder,
            out CapabilitiesServiceV2CallCounts capabilitiesServiceV2CallCounts,
            out ScriptServiceCallCounts scriptServiceCallCounts,
            out ScriptServiceV2CallCounts scriptServiceV2CallCounts,
            out FileTransferServiceCallCounts fileTransferServiceServiceCallCounts)
        {
            builder.LogAllCalls()
                .CountAllCalls(out var capabilitiesServiceCallCountsOut,
                    out var scriptServiceCallCountsOut,
                    out var scriptServiceV2CallCountsOut,
                    out var fileTransferServiceCallCountsOut);

            capabilitiesServiceV2CallCounts = capabilitiesServiceCallCountsOut;
            scriptServiceCallCounts = scriptServiceCallCountsOut;
            scriptServiceV2CallCounts = scriptServiceV2CallCountsOut;
            fileTransferServiceServiceCallCounts = fileTransferServiceCallCountsOut;

            return builder;
        }

        public static TentacleServiceDecoratorBuilder RecordAllExceptions(this TentacleServiceDecoratorBuilder builder,
            out CapabilitiesServiceV2Exceptions capabilitiesServiceV2Exceptions,
            out ScriptServiceExceptions scriptServiceExceptions,
            out ScriptServiceV2Exceptions scriptServiceV2Exceptions,
            out FileTransferServiceExceptions fileTransferServiceServiceExceptions)
        {
            builder.RecordExceptionThrownInCapabilitiesServiceV2(out var capabilitiesServiceV2ExceptionOut)
                .RecordExceptionThrownInScriptService(out var scriptServiceExceptionsOut)
                .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2ExceptionsOut)
                .RecordExceptionThrownInFileTransferService(out var fileTransferServiceExceptionsOut);

            capabilitiesServiceV2Exceptions = capabilitiesServiceV2ExceptionOut;
            scriptServiceExceptions = scriptServiceExceptionsOut;
            scriptServiceV2Exceptions = scriptServiceV2ExceptionsOut;
            fileTransferServiceServiceExceptions = fileTransferServiceExceptionsOut;

            return builder;
        }
    }
}