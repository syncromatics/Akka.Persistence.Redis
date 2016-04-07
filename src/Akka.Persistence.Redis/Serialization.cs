using System;
using Akka.Actor;
using RedisBoost.Core.Serialization;

namespace Akka.Persistence.Redis
{
    public class RedisPersistenceSerializer : BasicRedisSerializer
    {
        private readonly ActorSystem _system;

        public RedisPersistenceSerializer(ActorSystem system)
        {
            _system = system;
        }

        public override byte[] Serialize(object value)
        {
            var serializer = _system.Serialization.FindSerializerFor(value);
            return serializer.ToBinary(value);
        }

        public override object Deserialize(Type type, byte[] value)
        {
            var serializer = _system.Serialization.FindSerializerForType(type);
            return serializer.FromBinary(value, type);
        }
    }
}