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
                return MessagePackSerializer.Serialize(repr);

            if (obj is SelectedSnapshot snap)
                return MessagePackSerializer.Serialize(snap);

            return MessagePackSerializer.NonGeneric.Serialize(obj.GetType(), obj);
        }

        public override object FromBinary(byte[] bytes, Type type)
        {
            if (typeof(IPersistentRepresentation).IsAssignableFrom(type))
                return MessagePackSerializer.Deserialize<IPersistentRepresentation>(bytes);

            if (type == typeof(SelectedSnapshot))
                return MessagePackSerializer.Deserialize<SelectedSnapshot>(bytes);

            return MessagePackSerializer.NonGeneric.Deserialize(type, bytes);
        }

        public override int Identifier => 30;

        public override bool IncludeManifest => true;
    }
}
