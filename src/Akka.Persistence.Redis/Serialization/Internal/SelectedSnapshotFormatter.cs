using System;
using MessagePack;
using MessagePack.Formatters;

namespace Akka.Persistence.Redis.Serialization.Internal
{
    internal sealed class SelectedSnapshotFormatter<T> : IMessagePackFormatter<SelectedSnapshot> 
    {
        private static readonly IMessagePackFormatter<object> ObjectFormatter = TypelessFormatter.Instance;

        public int Serialize(ref byte[] bytes, int offset, SelectedSnapshot value, IFormatterResolver formatterResolver)
        {
            if (value == null)
                return MessagePackBinary.WriteNil(ref bytes, offset);

            var startOffset = offset;
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.Metadata.PersistenceId);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Metadata.SequenceNr);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Metadata.Timestamp.Ticks);
            offset += ObjectFormatter.Serialize(ref bytes, offset, value.Snapshot, formatterResolver);

            return offset - startOffset;
        }

        public SelectedSnapshot Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }

            string persistenceId = MessagePackBinary.ReadString(bytes, offset, out readSize);
            offset += readSize;
            long sequenceNr = MessagePackBinary.ReadInt64(bytes, offset, out readSize);
            offset += readSize;
            long timestampTicks = MessagePackBinary.ReadInt64(bytes, offset, out readSize);
            offset += readSize;
            object snapshot = ObjectFormatter.Deserialize(bytes, offset, formatterResolver, out readSize);
            offset += readSize;

            return new SelectedSnapshot(new SnapshotMetadata(persistenceId, sequenceNr, new DateTime(timestampTicks)), snapshot);
        }
    }
}