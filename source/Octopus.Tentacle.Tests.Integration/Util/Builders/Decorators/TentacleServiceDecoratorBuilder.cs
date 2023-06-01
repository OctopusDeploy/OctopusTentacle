using System;
using System.Collections.Generic;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class TentacleServiceDecoratorBuilder
    {

        private List<Func<IClientScriptService, IClientScriptService>> scriptServiceDecorator = new ();
        private List<Func<IClientScriptServiceV2, IClientScriptServiceV2>> scriptServiceV2Decorator = new ();
        private List<Func<IClientFileTransferService, IClientFileTransferService>> fileTransferServiceDecorator = new ();
        private List<Func<IClientCapabilitiesServiceV2, IClientCapabilitiesServiceV2>> capabilitiesServiceV2Decorator = new ();

        public TentacleServiceDecoratorBuilder DecorateScriptServiceWith(Func<IClientScriptService, IClientScriptService> scriptServiceDecorator)
        {
            this.scriptServiceDecorator.Add(scriptServiceDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Func<IClientScriptServiceV2, IClientScriptServiceV2> scriptServiceV2Decorator)
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

        public TentacleServiceDecoratorBuilder DecorateFileTransferServiceWith(Func<IClientFileTransferService, IClientFileTransferService> fileTransferServiceDecorator)
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

        public TentacleServiceDecoratorBuilder DecorateCapabilitiesServiceV2With(Func<IClientCapabilitiesServiceV2, IClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
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

        public ITentacleServiceDecorator Build()
        {
            return new FooTentacleServiceDecorator(Combine(scriptServiceDecorator), Combine(scriptServiceV2Decorator), Combine(fileTransferServiceDecorator), Combine(capabilitiesServiceV2Decorator));
        }

        public static Func<T, T> Combine<T>(List<Func<T, T>> chain) where T : class
        {

            return t =>
            {
                var reverseChain = new List<Func<T, T>>(chain);
                reverseChain.Reverse();

                foreach (var func in reverseChain)
                {
                    t = func(t);
                }

                return t;
            };
        }

        private class FooTentacleServiceDecorator : ITentacleServiceDecorator
        {
            private Func<IClientScriptService, IClientScriptService> scriptServiceDecorator;
            private Func<IClientScriptServiceV2, IClientScriptServiceV2> scriptServiceV2Decorator;
            private Func<IClientFileTransferService, IClientFileTransferService> fileTransferServiceDecorator;
            private Func<IClientCapabilitiesServiceV2, IClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator;

            public FooTentacleServiceDecorator(Func<IClientScriptService, IClientScriptService> scriptServiceDecorator, 
                Func<IClientScriptServiceV2, IClientScriptServiceV2> scriptServiceV2Decorator,
                Func<IClientFileTransferService, IClientFileTransferService> fileTransferServiceDecorator,
                Func<IClientCapabilitiesServiceV2, IClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
            {
                this.scriptServiceDecorator = scriptServiceDecorator;
                this.scriptServiceV2Decorator = scriptServiceV2Decorator;
                this.fileTransferServiceDecorator = fileTransferServiceDecorator;
                this.capabilitiesServiceV2Decorator = capabilitiesServiceV2Decorator;
            }

            public IClientScriptService Decorate(IClientScriptService scriptService)
            {
                return scriptServiceDecorator(scriptService);
            }

            public IClientScriptServiceV2 Decorate(IClientScriptServiceV2 scriptService)
            {
                return scriptServiceV2Decorator(scriptService);
            }

            public IClientFileTransferService Decorate(IClientFileTransferService service)
            {
                return fileTransferServiceDecorator(service);
            }

            public IClientCapabilitiesServiceV2 Decorate(IClientCapabilitiesServiceV2 service)
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