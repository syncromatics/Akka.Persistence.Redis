using Akka.Actor;
using MessagePack;
using MessagePack.Formatters;

namespace Akka.Persistence.Redis.Serialization.Internal
{
    public sealed class ActorPathFormatter<T> : IMessagePackFormatter<T> where T : ActorPath
    {
        public int Serialize(ref byte[] bytes, int offset, T value, IFormatterResolver formatterResolver)
        {
            if (value == null)
                return MessagePackBinary.WriteNil(ref bytes, offset);

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