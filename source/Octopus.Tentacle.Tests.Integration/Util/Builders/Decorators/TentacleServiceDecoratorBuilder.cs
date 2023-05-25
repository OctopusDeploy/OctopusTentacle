using System;
using System.Collections.Generic;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class TentacleServiceDecoratorBuilder
    {

        private List<Func<IScriptService, IScriptService>> scriptServiceDecorator = new ();
        private List<Func<IScriptServiceV2, IScriptServiceV2>> scriptServiceV2Decorator = new ();
        private List<Func<IFileTransferService, IFileTransferService>> fileTransferServiceDecorator = new ();
        private List<Func<ICapabilitiesServiceV2, ICapabilitiesServiceV2>> capabilitiesServiceV2Decorator = new ();

        public TentacleServiceDecoratorBuilder DecorateScriptServiceWith(Func<IScriptService, IScriptService> scriptServiceDecorator)
        {
            this.scriptServiceDecorator.Add(scriptServiceDecorator);
            return this;
        }
        
        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Func<IScriptServiceV2, IScriptServiceV2> scriptServiceV2Decorator)
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
        
        public TentacleServiceDecoratorBuilder DecorateCapabilitiesServiceV2With(Func<ICapabilitiesServiceV2, ICapabilitiesServiceV2> capabilitiesServiceV2Decorator)
        {
            this.capabilitiesServiceV2Decorator.Add(capabilitiesServiceV2Decorator);
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
            private Func<IScriptService, IScriptService> scriptServiceDecorator;
            private Func<IScriptServiceV2, IScriptServiceV2> scriptServiceV2Decorator;
            private Func<IFileTransferService, IFileTransferService> fileTransferServiceDecorator;
            private Func<ICapabilitiesServiceV2, ICapabilitiesServiceV2> capabilitiesServiceV2Decorator;

            public FooTentacleServiceDecorator(Func<IScriptService, IScriptService> scriptServiceDecorator, Func<IScriptServiceV2, IScriptServiceV2> scriptServiceV2Decorator, Func<IFileTransferService, IFileTransferService> fileTransferServiceDecorator, Func<ICapabilitiesServiceV2, ICapabilitiesServiceV2> capabilitiesServiceV2Decorator)
            {
                this.scriptServiceDecorator = scriptServiceDecorator;
                this.scriptServiceV2Decorator = scriptServiceV2Decorator;
                this.fileTransferServiceDecorator = fileTransferServiceDecorator;
                this.capabilitiesServiceV2Decorator = capabilitiesServiceV2Decorator;
            }

            public IScriptService Decorate(IScriptService scriptService)
            {
                return scriptServiceDecorator(scriptService);
            }

            public IScriptServiceV2 Decorate(IScriptServiceV2 scriptService)
            {
                return scriptServiceV2Decorator(scriptService);
            }

            public IFileTransferService Decorate(IFileTransferService service)
            {
                return fileTransferServiceDecorator(service);
            }

            public ICapabilitiesServiceV2 Decorate(ICapabilitiesServiceV2 service)
            {
                return capabilitiesServiceV2Decorator(service);
            }
        }
    }
}