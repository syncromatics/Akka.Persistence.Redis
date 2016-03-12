using System;
using Akka.Configuration;
using Akka.Persistence.Redis.Snapshot;
using Akka.Persistence.TestKit.Snapshot;
using Akka.Util.Internal;
using RedisBoost;
using Xunit.Abstractions;

namespace Akka.Persistence.Redis.Tests
{
    public class RedisSnapshotStoreSpec : SnapshotStoreSpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static readonly string ConfigFormat = @"akka.persistence.snapshot-store {{
            plugin = ""akka.persistence.snapshot-store.redis""
            redis {{ 
		        class = ""Akka.Persistence.Redis.Snapshot.RedisSnapshotStore, Akka.Persistence.Redis"",
		        dispatcher = ""akka.actor.default-dispatcher""
		        connection-string=""data source = localhost:6379;initial catalog={0}""
            }}
        }}";

        public RedisSnapshotStoreSpec(ITestOutputHelper output)
            : base(ConfigurationFactory.ParseString(string.Format(ConfigFormat, Counter.IncrementAndGet())), "RedisSnapshotStoreSpec", output: output)
        {
            TestSetup.Cleanup(RedisPersistence.Get(Sys).SnapshotStoreSettings);
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            TestSetup.Cleanup(RedisPersistence.Get(Sys).SnapshotStoreSettings);
        }
    }
}