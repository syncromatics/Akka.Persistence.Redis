using System.Linq;
using System.Threading.Tasks;
using Akka.Persistence.Snapshot;
using RedisBoost;
using RedisBoost.Core.Serialization;

namespace Akka.Persistence.Redis.Snapshot
{
    public class RedisSnapshotStore : SnapshotStore
    {
        private readonly RedisSettings _settings = RedisPersistence.Get(Context.System).SnapshotStoreSettings;
        private readonly IRedisClientsPool _pool;
        private readonly BasicRedisSerializer _serializer;

        public RedisSnapshotStore()
        {
            _serializer = new RedisPersistenceSerializer(Context.System);
            _pool = RedisClient.CreateClientsPool();
        }

        protected override void PostStop()
        {
            base.PostStop();
            _pool.Dispose();
        }

        protected override async Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var client = await CreateRedisClientAsync();
            var snapshots = await client.ZRevRangeAsync(SnapshotKey(persistenceId), 0L, criteria.MaxSequenceNr);
            var found = snapshots
                .Select(bulk => bulk.As<SelectedSnapshot>())
                .FirstOrDefault(snapshot => snapshot.Metadata.Timestamp <= criteria.MaxTimeStamp && snapshot.Metadata.SequenceNr <= criteria.MaxSequenceNr);

            return found;
        }

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            var client = await CreateRedisClientAsync();
            var selectedSnapshot = new SelectedSnapshot(metadata, snapshot);
            await client.ZAddAsync(SnapshotKey(metadata.PersistenceId), metadata.SequenceNr, selectedSnapshot);
        }

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
        {
            var client = await CreateRedisClientAsync();
            await client.ZRemRangeByScoreAsync(SnapshotKey(metadata.PersistenceId), metadata.SequenceNr, metadata.SequenceNr);
        }

        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var client = await CreateRedisClientAsync();
            var snapshotKey = SnapshotKey(persistenceId);
            var snapshots = await client.ZRevRangeAsync(snapshotKey, 0L, criteria.MaxSequenceNr);
            var found = snapshots
                .Where(bulk => bulk.ResponseType == ResponseType.Bulk)
                .Select(bulk => bulk.As<SelectedSnapshot>())
                .Where(snapshot => snapshot.Metadata.Timestamp <= criteria.MaxTimeStamp && snapshot.Metadata.SequenceNr <= criteria.MaxSequenceNr)
                .ToArray();
            await client.ZRemAsync(snapshotKey, found);
        }

        private string SnapshotKey(string persistenceId) => _settings.KeyPrefix + ":" + persistenceId;

        private Task<IRedisClient> CreateRedisClientAsync()
        {
            return _pool.CreateClientAsync(_settings.ConnectionString, _serializer);
        }
    }
}