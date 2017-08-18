using System;
using System.Collections.Generic;
using Akka.Actor;
using MessagePack;
using MessagePack.Formatters;

namespace Akka.Persistence.Redis.Serialization.Internal
{
    internal sealed class AkkaResolver : IFormatterResolver
    {
        public static IFormatterResolver Instance = new AkkaResolver();
        private AkkaResolver() { }
        public IMessagePackFormatter<T> GetFormatter<T>() => FormatterCache<T>.Formatter;

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;
            static FormatterCache() => Formatter = (IMessagePackFormatter<T>)AkkaResolverGetFormatterHelper.GetFormatter(typeof(T));
        }
    }

    internal static class AkkaResolverGetFormatterHelper
    {
        private static readonly Dictionary<Type, object> FormatterMap = new Dictionary<Type, object>
        {
            {typeof(ActorPath), new ActorPathFormatter<ActorPath>()},
            {typeof(ChildActorPath), new ActorPathFormatter<ChildActorPath>()},
            {typeof(RootActorPath), new ActorPathFormatter<RootActorPath>()},
            {typeof(IPersistentRepresentation), new PersistentFormatter<IPersistentRepresentation>()},
            {typeof(Persistent), new PersistentFormatter<Persistent>()},
            {typeof(SelectedSnapshot), new SelectedSnapshotFormatter<SelectedSnapshot>()},
        };

        internal static object GetFormatter(Type t) => FormatterMap.TryGetValue(t, out var formatter) ? formatter : null;
    }
}