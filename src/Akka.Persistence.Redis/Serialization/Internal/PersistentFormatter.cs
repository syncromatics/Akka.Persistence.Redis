using MessagePack;
using MessagePack.Formatters;

namespace Akka.Persistence.Redis.Serialization.Internal
{
    internal sealed class PersistentFormatter<T> : IMessagePackFormatter<T> where T: IPersistentRepresentation
    {
        private static readonly IMessagePackFormatter<object> ObjectFormatter = TypelessFormatter.Instance;

        public int Serialize(ref byte[] bytes, int offset, T value, IFormatterResolver formatterResolver)
        {
            if (value == null)
                return MessagePackBinary.WriteNil(ref bytes, offset);

            var startOffset = offset;
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.PersistenceId);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.SequenceNr);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.WriterGuid);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.Manifest);
            offset += ObjectFormatter.Serialize(ref bytes, offset, value.Payload, formatterResolver);

            return offset - startOffset;
        }

        public T Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return default(T);
            }

            string persistenceId = MessagePackBinary.ReadString(bytes, offset, out readSize);
            offset += readSize;
            long sequenceNr = MessagePackBinary.ReadInt64(bytes, offset, out readSize);
            offset += readSize;
            string writerGuid = MessagePackBinary.ReadString(bytes, offset, out readSize);
            offset += readSize;
            string manifest = MessagePackBinary.ReadString(bytes, offset, out readSize);
            offset += readSize;
            object payload = ObjectFormatter.Deserialize(bytes, offset, formatterResolver, out readSize);
            offset += readSize;

            IPersistentRepresentation persistent = new Persistent(payload, sequenceNr, persistenceId, manifest, false, null, writerGuid);
            return (T)persistent;
        }
    }
}