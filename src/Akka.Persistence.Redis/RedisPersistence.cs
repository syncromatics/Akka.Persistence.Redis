using System;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Persistence.Redis
{
    public class RedisSettings
    {
        public static RedisSettings Create(Config config)
        {
            return new RedisSettings(
                connectionString: config.GetString("connection-string"),
                keyPrefix: config.GetString("key-prefix"));
        }

        public readonly string ConnectionString;
        public readonly string KeyPrefix;

        public RedisSettings(string connectionString,  string keyPrefix)
        {
            ConnectionString = connectionString;
            KeyPrefix = keyPrefix;
        }
    }

    public class RedisPersistence : IExtension
    {
        public static RedisPersistence Get(ActorSystem system) => system.WithExtension<RedisPersistence, RedisPersistenceProvider>();
        public static Config DefaultConfig() => ConfigurationFactory.FromResource<RedisPersistence>("Akka.Persistence.Redis.reference.conf");

        public readonly RedisSettings JournalSettings;
        public readonly RedisSettings SnapshotStoreSettings;

        public RedisPersistence(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfig());
            
            JournalSettings = RedisSettings.Create(system.Settings.Config.GetConfig("akka.persistence.journal.redis"));
            SnapshotStoreSettings = RedisSettings.Create(system.Settings.Config.GetConfig("akka.persistence.snapshot-store.redis"));
        }
    }

    public class RedisPersistenceProvider : ExtensionIdProvider<RedisPersistence>
    {
        public override RedisPersistence CreateExtension(ExtendedActorSystem system)
        {
            return new RedisPersistence(system);
        }
    }
}