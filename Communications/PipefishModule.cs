using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using NuGet;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Communications.Agentless;
using Octopus.Shared.Communications.Encryption;
using Octopus.Shared.Communications.Integration;
using Octopus.Shared.FileTransfer;
using Pipefish;
using Pipefish.Core;
using Pipefish.Hosting;
using Pipefish.Persistence;
using Pipefish.Persistence.Filesystem;
using Pipefish.Streaming;
using Pipefish.Transport;
using Pipefish.Transport.Filesystem;
using Pipefish.Util.Storage;
using Pipefish.WellKnown.Dispatch;
using Module = Autofac.Module;

namespace Octopus.Shared.Communications
{
    public class PipefishModule : Module
    {
        readonly Assembly[] assemblies;

        public PipefishModule(params Assembly[] assemblies)
        {
            this.assemblies = assemblies.Concat(new[] { typeof(Actor).Assembly, typeof(FileSender).Assembly }).ToArray();
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            var logger = Log.Octopus();
            Pipefish.Diagnostics.Log.OnDebug(logger.Trace);
            Pipefish.Diagnostics.Log.OnError((e, m) => logger.Error(e.UnpackFromContainers(), m));

            builder.RegisterAssemblyTypes(assemblies)
                .Where(IsCreatedByMessage)
                .As(t => t
                    .GetInterfaces()
                    .Where(IsCreatedByMessage)
                    .Select(i => new KeyedService(
                        KeyFor(t, MessageTypeName.For(i.GetGenericArguments()[0])),
                        typeof(IActor))));

            builder.RegisterType<AutofacActorFactory>().As<IActorFactory>();

            builder.RegisterAssemblyTypes(assemblies)
                .Where(t => t.GetCustomAttributes(typeof(WellKnownActorAttribute), false).Any())
                .As<IActor>()
                .WithMetadataFrom<WellKnownActorAttribute>();

            builder.RegisterType<MessageInspectorCollection>()
                .Named<IMessageInspector>("collection");

            builder.Register(c => new ActivitySpace(c.Resolve<ICommunicationsConfiguration>().Squid, c.Resolve<IMessageStore>(), c.ResolveNamed<IMessageInspector>("collection")))
                .AsSelf()
                .As<IActivitySpace>()
                .OnActivating(e => ActivitySpaceStarter.LoadWellKnownActors(e.Instance, e.Context))
                .SingleInstance();

            builder.RegisterType<ActivitySpaceStarter>()
                .As<IActivitySpaceStarter>()
                .SingleInstance();

            builder.Register(c => new InMemoryMessageStore())
                .As<IMessageStore>()
                .SingleInstance();

            builder.Register(c => new StreamStore(
                    c.Resolve<ICommunicationsConfiguration>().StreamsDirectory, c.Resolve<IOctopusFileSystem>()))
                .As<IOctopusStreamStore>()
                .As<IStreamStore>()
                .SingleInstance();

            builder.Register(c => new DirectoryActorStorage(
                    c.Resolve<ICommunicationsConfiguration>().ActorStateDirectory,
                    c.Resolve<IStorageStreamTransform>()))
                .As<IActorStorage>()
                .SingleInstance();

            builder.RegisterType<EncryptedStorageStream>().As<IStorageStreamTransform>();

            builder.RegisterType<ShutdownToken>().SingleInstance();
        }

        static string KeyFor(Type type, string createdByMessageTypeName)
        {
            var attr = type.GetCustomAttribute<AgentlessOverrideAttribute>();
            if (attr == null)
                return createdByMessageTypeName;
            return createdByMessageTypeName + "+" + attr.CommunicationStyle;
        }

        static bool IsCreatedByMessage(Type t)
        {
            return t.IsClosedTypeOf(typeof(ICreatedBy<>)) || t.IsClosedTypeOf(typeof(ICreatedByAsync<>));
        }
    }
}
