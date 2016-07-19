//-----------------------------------------------------------------------
// <copyright file="RedisSnapshotStore.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Snapshot;
using Akka.Serialization;
using Akka.Util.Internal;
using StackExchange.Redis;

namespace Akka.Persistence.Redis.Snapshot
{
    public class RedisSnapshotStore : SnapshotStore
    {
        private readonly RedisSettings _settings = RedisPersistence.Get(Context.System).SnapshotStoreSettings;
        private Lazy<Serializer> _serializer;
        private Lazy<IDatabase> _database;
        private ActorSystem _system;

        protected override void PreStart()
        {
            base.PreStart();

            _system = Context.System;
            _database = new Lazy<IDatabase>(() =>
            {
                var redisConnection = ConnectionMultiplexer.Connect(_settings.ConfigurationString);
                return redisConnection.GetDatabase(_settings.Database);
            });
            _serializer = new Lazy<Serializer>(() => _system.Serialization.FindSerializerForType(typeof(SelectedSnapshot)));
        }

        protected override async Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var snapshots = await _database.Value.SortedSetRangeByScoreAsync(SnapshotKey(persistenceId), criteria.MaxSequenceNr, -1, Exclude.None, Order.Descending);
            var found = snapshots
                .Select(c => _serializer.Value.FromBinary(c, typeof(SelectedSnapshot)).AsInstanceOf<SelectedSnapshot>())
                .FirstOrDefault(snapshot => snapshot.Metadata.Timestamp <= criteria.MaxTimeStamp && snapshot.Metadata.SequenceNr <= criteria.MaxSequenceNr);

            return found;
        }

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            var snapshotRecord = _serializer.Value.ToBinary(new SelectedSnapshot(metadata, snapshot));
            if (snapshotRecord != null)
                await _database.Value.SortedSetAddAsync(SnapshotKey(metadata.PersistenceId), snapshotRecord, metadata.SequenceNr);
            else
                throw new Exception($"Failed to save snapshot. metadata: {metadata} snapshot: {snapshot}");
        }

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
        {
            await _database.Value.SortedSetRemoveRangeByScoreAsync(SnapshotKey(metadata.PersistenceId), metadata.SequenceNr, metadata.SequenceNr);
        }

        // TODO: SnapshotSelectionCriteria should have MinSequenceNr
        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var snapshots = await _database.Value.SortedSetRangeByScoreAsync(SnapshotKey(persistenceId), criteria.MaxSequenceNr, 0L, Exclude.None, Order.Descending);
            var found = snapshots
                .Select(c => _serializer.Value.FromBinary(c, typeof(SelectedSnapshot)).AsInstanceOf<SelectedSnapshot>())
                .Where(snapshot => snapshot.Metadata.Timestamp <= criteria.MaxTimeStamp && snapshot.Metadata.SequenceNr <= criteria.MaxSequenceNr)
                .Select(s => _database.Value.SortedSetRemoveRangeByScoreAsync(SnapshotKey(persistenceId), s.Metadata.SequenceNr, s.Metadata.SequenceNr))
                .ToArray();

            await Task.WhenAll(found);
        }

        private string SnapshotKey(string persistenceId) => $"{_settings.KeyPrefix}:{persistenceId}";
    }
}