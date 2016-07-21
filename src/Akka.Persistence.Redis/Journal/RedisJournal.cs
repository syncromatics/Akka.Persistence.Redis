//-----------------------------------------------------------------------
// <copyright file="RedisJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using Akka.Serialization;
using Akka.Util.Internal;
using StackExchange.Redis;

namespace Akka.Persistence.Redis.Journal
{
    public class RedisJournal : AsyncWriteJournal
    {
        private readonly RedisSettings _settings = RedisPersistence.Get(Context.System).JournalSettings;
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
            _serializer = new Lazy<Serializer>(() => _system.Serialization.FindSerializerForType(typeof(IPersistentRepresentation)));
        }

        public override async Task ReplayMessagesAsync(
            IActorContext context,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max,
            Action<IPersistentRepresentation> recoveryCallback)
        {
            RedisValue[] journals = await _database.Value.SortedSetRangeByScoreAsync(GetJournalKey(persistenceId), fromSequenceNr, toSequenceNr, skip: 0L, take: max);

            foreach (var journal in journals)
            {
                var record = _serializer.Value.FromBinary(journal, typeof(IPersistentRepresentation)).AsInstanceOf<IPersistentRepresentation>();
                if (record != null)
                    recoveryCallback(record);
                else
                    throw new Exception($"{nameof(ReplayMessagesAsync)}: Failed to deserialize {nameof(IPersistentRepresentation)}");
            }
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            var highestSequenceNr = await _database.Value.StringGetAsync(GetHighestSequenceNrKey(persistenceId));
            return highestSequenceNr.IsNull ? 0L : (long)highestSequenceNr;
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            await _database.Value.SortedSetRemoveRangeByScoreAsync(GetJournalKey(persistenceId), -1, toSequenceNr);
        }

        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var messagesList = messages.ToList();
            var groupedTasks = messagesList.GroupBy(m => m.PersistenceId).ToDictionary(g => g.Key,
                async g =>
                {
                    var persistentMessages = g.SelectMany(aw => (IImmutableList<IPersistentRepresentation>)aw.Payload).ToList();

                    var currentMaxSeqNumber = (long)_database.Value.StringGet(GetHighestSequenceNrKey(g.Key));

                    var transaction = _database.Value.CreateTransaction();
                    foreach (var write in persistentMessages)
                    {
                        if (write.SequenceNr > currentMaxSeqNumber)
                        {
                            currentMaxSeqNumber = write.SequenceNr;
                        }

#pragma warning disable 4014
                        transaction.SortedSetAddAsync(GetJournalKey(write.PersistenceId), _serializer.Value.ToBinary(write), write.SequenceNr);
#pragma warning restore 4014
                    }

#pragma warning disable 4014
                    transaction.StringSetAsync(GetHighestSequenceNrKey(g.Key), currentMaxSeqNumber);
#pragma warning restore 4014

                    if (!await transaction.ExecuteAsync())
                    {
                        throw new Exception($"{nameof(WriteMessagesAsync)}: failed to write {typeof(IPersistentRepresentation).Name} to redis");
                    }
                });

            return await Task<IImmutableList<Exception>>.Factory.ContinueWhenAll(
                    groupedTasks.Values.ToArray(),
                    tasks => messagesList.Select(
                        m =>
                        {
                            var task = groupedTasks[m.PersistenceId];
                            return task.IsFaulted ? TryUnwrapException(task.Exception) : null;
                        }).ToImmutableList());
        }

        private RedisKey GetJournalKey(string persistenceId) => $"{_settings.KeyPrefix}:{persistenceId}";

        private RedisKey GetHighestSequenceNrKey(string persistenceId)
        {
            return $"{GetJournalKey(persistenceId)}.highestSequenceNr";
        }

        private static Exception UnwrapException(Exception e)
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
    }
}