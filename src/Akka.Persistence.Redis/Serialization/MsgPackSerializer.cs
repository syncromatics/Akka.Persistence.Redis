//-----------------------------------------------------------------------
// <copyright file="MsgPackSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2017 Akka.NET Contrib <https://github.com/AkkaNetContrib/Akka.Persistence.Redis>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Reflection;
using Akka.Actor;
using Akka.Persistence.Redis.Serialization.Internal;
using Akka.Serialization;
using MessagePack;
using MessagePack.Resolvers;

namespace Akka.Persistence.Redis.Serialization
{
    public class MsgPackSerializer : Serializer
    {
        static MsgPackSerializer()
        {
            CompositeResolver.RegisterAndSetAsDefault(
                AkkaResolver.Instance,
                OldSpecResolver.Instance, // Redis compatible MsgPack spec
                TypelessContractlessStandardResolver.Instance);
        }

        public MsgPackSerializer(ExtendedActorSystem system) : base(system)
        {
        }

        public override byte[] ToBinary(object obj)
        {
            if (obj is IPersistentRepresentation repr)
                return PersistenceMessageSerializer(repr);

            if (obj is SelectedSnapshot snap)
                return SelectedSnapshotSerializer(snap);

            return MessagePackSerializer.NonGeneric.Serialize(obj.GetType(), obj);
        }

        public override object FromBinary(byte[] bytes, Type type)
        {
            if (typeof(IPersistentRepresentation).IsAssignableFrom(type))
                return PersistenceMessageDeserializer(bytes);

            if (type == typeof(SelectedSnapshot))
                return SelectedSnapshotDeserializer(bytes);

            return MessagePackSerializer.NonGeneric.Deserialize(type, bytes);
        }

        public override int Identifier => 30;

        public override bool IncludeManifest => true;

        private byte[] PersistenceMessageSerializer(IPersistentRepresentation obj)
        {
            var persistenceMessage = new PersistenceMessage(
                obj.PersistenceId,
                obj.SequenceNr,
                obj.WriterGuid,
                obj.Manifest,
                obj.Payload);

            return MessagePackSerializer.Serialize(persistenceMessage);
        }

        private IPersistentRepresentation PersistenceMessageDeserializer(byte[] bytes)
        {
            var persistenceMessage = MessagePackSerializer.Deserialize<PersistenceMessage>(bytes);

            return new Persistent(
                persistenceMessage.Payload,
                persistenceMessage.SequenceNr,
                persistenceMessage.PersistenceId,
                persistenceMessage.Manifest,
                false,
                null,
                persistenceMessage.WriterGuid);
        }

        private byte[] SelectedSnapshotSerializer(SelectedSnapshot obj)
        {
            var snapshotMessage = new SnapshotMessage(
                obj.Metadata.PersistenceId,
                obj.Metadata.SequenceNr,
                obj.Metadata.Timestamp.Ticks,
                obj.Snapshot);

            return MessagePackSerializer.Serialize(snapshotMessage);
        }

        private SelectedSnapshot SelectedSnapshotDeserializer(byte[] bytes)
        {
            var snapshotMessage = MessagePackSerializer.Deserialize<SnapshotMessage>(bytes);
            var metadata = new SnapshotMetadata(
                snapshotMessage.PersistenceId,
                snapshotMessage.SequenceNr,
                new DateTime(snapshotMessage.Timestamp));
            return new SelectedSnapshot(metadata, snapshotMessage.Snapshot);
        }
    }

    #region Messages
    [MessagePackObject]
    public sealed class PersistenceMessage
    {
        [SerializationConstructor]
        public PersistenceMessage(string persistenceId, long sequenceNr, string writerGuid, string manifest, object payload)
        {
            PersistenceId = persistenceId;
            SequenceNr = sequenceNr;
            WriterGuid = writerGuid;
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
        public string Manifest { get; }

        [Key(4)]
        public object Payload { get; }
    }

    [MessagePackObject]
    public sealed class SnapshotMessage
    {
        public SnapshotMessage(string persistenceId, long sequenceNr, long timestamp, object snapshot)
        {
            PersistenceId = persistenceId;
            SequenceNr = sequenceNr;
            Timestamp = timestamp;
            Snapshot = snapshot;
        }

        [Key(0)]
        public string PersistenceId { get; }

        [Key(1)]
        public long SequenceNr { get; }

        [Key(2)]
        public long Timestamp { get; }

        [Key(3)]
        public object Snapshot { get; }
    }
    #endregion
}
