using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using RedisBoost;
using RedisBoost.Core.Serialization;

namespace Akka.Persistence.Redis.Journal
{
    public class RedisJournal : AsyncWriteJournal
    {
        private readonly RedisSettings _settings = RedisPersistence.Get(Context.System).JournalSettings;
        private readonly IRedisClientsPool _pool;
        private readonly BasicRedisSerializer _serializer;

        public RedisJournal()
        {
            _serializer = new RedisPersistenceSerializer(Context.System);
            _pool = RedisClient.CreateClientsPool();
        }

        protected override void PostStop()
        {
            base.PostStop();

            _pool.Dispose();
        }

        public override async Task ReplayMessagesAsync(IActorContext context, string persistenceId, long fromSequenceNr, long toSequenceNr, long max,
            Action<IPersistentRepresentation> recoveryCallback)
        {
            var client = await CreateRedisClientAsync();
            var multiBulk = await client.ZRangeByScoreAsync(JournalKey(persistenceId), fromSequenceNr, toSequenceNr, 0L, max);
            if (!multiBulk.IsNull)
            {
                var persistents = multiBulk.Select(bulk => bulk.As<IPersistentRepresentation>());
                foreach (var persistent in persistents)
                {
                    recoveryCallback(persistent);
                }
            }
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            var client = await CreateRedisClientAsync();
            var highestSequenceNr = await client.GetAsync(HighestSequenceNrKey(persistenceId));
            return highestSequenceNr.IsNull
                ? 0L
                : highestSequenceNr.As<long>();
        }

        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var writeTasks = messages.Select(write => Task.Run(async () =>
            {
                var client = await CreateRedisClientAsync();
                await client.MultiAsync();
                var payloads = (IImmutableList<IPersistentRepresentation>)write.Payload;
                foreach (var payload in payloads)
                {
                    await client.ZAddAsync(JournalKey(payload.PersistenceId), payload.SequenceNr, payload);
                    await client.SetAsync(HighestSequenceNrKey(payload.PersistenceId), payload.SequenceNr);
                }
                await client.ExecAsync();
            }))
            .ToArray();

            return await Task<IImmutableList<Exception>>
                .Factory
                .ContinueWhenAll(writeTasks, tasks => tasks
                        .Select(t => t.IsFaulted ? UnwrapException(t.Exception) : null)
                        .ToImmutableArray());
        }

        private Exception UnwrapException(Exception e)
        {
            var aggregateException = e as AggregateException;
            if (aggregateException != null)
            {
                aggregateException = aggregateException.Flatten();
                if (aggregateException.InnerExceptions.Count == 1)
                    return aggregateException.InnerExceptions[0];
            }
            return e;
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            var client = await CreateRedisClientAsync();
            await client.ZRemRangeByScoreAsync(JournalKey(persistenceId), -1, toSequenceNr);
        }

        private string HighestSequenceNrKey(string persistenceId) => JournalKey(persistenceId) + ".highestSequenceNr";
        private string JournalKey(string persistenceId) => _settings.KeyPrefix + ":" + persistenceId;

        private Task<IRedisClient> CreateRedisClientAsync()
        {
            return _pool.CreateClientAsync(_settings.ConnectionString, _serializer);
        }
    }
}