//-----------------------------------------------------------------------
// <copyright file="MsgPackSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2017 Akka.NET Contrib <https://github.com/AkkaNetContrib/Akka.Persistence.Redis>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using Akka.Actor;
using Akka.Serialization;
using Akka.Util;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Akka.Persistence.Redis.Serialization
{
    public class MsgPackSerializer : Serializer
    {
        #region Messages
        [MessagePackObject]
        public sealed class PersistenceMessage
        {
            [SerializationConstructor]
            public PersistenceMessage(string persistenceId, long sequenceNr, string writerGuid, int serializerId, string manifest, byte[] payload)
            {
                PersistenceId = persistenceId;
                SequenceNr = sequenceNr;
                WriterGuid = writerGuid;
                SerializerId = serializerId;
                Manifest = manifest;
                Payload = payload;
            }

            [Key(0)]
            public string PersistenceId { get; }

            [Key(1)]
            public long SequenceNr { get; }

            [Key(2)]
            public string WriterGuid { get; }

            [Key(3)]
            public int SerializerId { get; }

            [Key(4)]
            public string Manifest { get; }

            [Key(5)]
            public byte[] Payload { get; }
        }
        #endregion

        static MsgPackSerializer()
        {
            CompositeResolver.RegisterAndSetAsDefault(
                ActorPathResolver.Instance,
                TypelessContractlessStandardResolver.Instance);
        }

        public MsgPackSerializer(ExtendedActorSystem system) : base(system)
        {
        }

        public override byte[] ToBinary(object obj)
        {
            if (obj is IPersistentRepresentation repr)
                return PersistenceMessageSerializer(repr);

            return MessagePackSerializer.NonGeneric.Serialize(obj.GetType(), obj);
        }

        public override object FromBinary(byte[] bytes, Type type)
        {
            if (typeof(IPersistentRepresentation).IsAssignableFrom(type))
                return PersistenceMessageDeserializer(bytes);

            return MessagePackSerializer.NonGeneric.Deserialize(type, bytes);
        }

        public override int Identifier => 30;

        public override bool IncludeManifest => true;

        private byte[] PersistenceMessageSerializer(IPersistentRepresentation obj)
        {
            var serializer = system.Serialization.FindSerializerFor(obj.Payload);

            // get manifest
            var manifestSerializer = serializer as SerializerWithStringManifest;
            var payloadManifest = "";

            if (manifestSerializer != null)
            {
                var manifest = manifestSerializer.Manifest(obj.Payload);
                if (!string.IsNullOrEmpty(manifest))
                    payloadManifest = manifest;
            }
            else
            {
                if (serializer.IncludeManifest)
                    payloadManifest = obj.Payload.GetType().TypeQualifiedName();
            }

            var persistenceMessage = new PersistenceMessage(
                obj.PersistenceId,
                obj.SequenceNr,
                obj.WriterGuid,
                serializer.Identifier,
                payloadManifest,
                serializer.ToBinary(obj.Payload));

            return MessagePackSerializer.Serialize(persistenceMessage);
        }

        private IPersistentRepresentation PersistenceMessageDeserializer(byte[] bytes)
        {
            var persistenceMessage = MessagePackSerializer.Deserialize<PersistenceMessage>(bytes);

            var payload = system.Serialization.Deserialize(
                persistenceMessage.Payload,
                persistenceMessage.SerializerId,
                persistenceMessage.Manifest);

            return new Persistent(
                payload,
                persistenceMessage.SequenceNr,
                persistenceMessage.PersistenceId,
                persistenceMessage.Manifest,
                false,
                null,
                persistenceMessage.WriterGuid);
        }
    }

    public class ActorPathResolver : IFormatterResolver
    {
        public static IFormatterResolver Instance = new ActorPathResolver();
        private ActorPathResolver() { }
        public IMessagePackFormatter<T> GetFormatter<T>() => FormatterCache<T>.Formatter;

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;
            static FormatterCache() => Formatter = (IMessagePackFormatter<T>)ActorPathResolverGetFormatterHelper.GetFormatter(typeof(T));
        }
    }

    internal static class ActorPathResolverGetFormatterHelper
    {
        private static readonly Dictionary<Type, object> FormatterMap = new Dictionary<Type, object>
        {
            {typeof(ActorPath), new ActorPathFormatter<ActorPath>()},
            {typeof(ChildActorPath), new ActorPathFormatter<ChildActorPath>()},
            {typeof(RootActorPath), new ActorPathFormatter<RootActorPath>()}
        };

        internal static object GetFormatter(Type t) => FormatterMap.TryGetValue(t, out var formatter) ? formatter : null;
    }

    public class ActorPathFormatter<T> : IMessagePackFormatter<T> where T : ActorPath
    {
        public int Serialize(ref byte[] bytes, int offset, T value, IFormatterResolver formatterResolver)
        {
            if (value == null)
            {
                return MessagePackBinary.WriteNil(ref bytes, offset);
            }

            var startOffset = offset;
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.ToSerializationFormat());
            return offset - startOffset;
        }

        public T Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }

            var path = MessagePackBinary.ReadString(bytes, offset, out readSize);
            return ActorPath.TryParse(path, out var actorPath) ? (T)actorPath : null;
        }
    }
}
