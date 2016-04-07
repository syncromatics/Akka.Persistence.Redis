using System;
using Akka.Configuration;
using Akka.Persistence.TestKit.Journal;
using Akka.Util.Internal;
using RedisBoost;
using Xunit.Abstractions;

namespace Akka.Persistence.Redis.Tests
{
    public class RedisJournalSpec : JournalSpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static readonly string ConfigFormat = @"akka.persistence.journal {{
            plugin = ""akka.persistence.journal.redis""
            redis {{
		        class = ""Akka.Persistence.Redis.Journal.RedisJournal, Akka.Persistence.Redis"",
		        dispatcher = ""akka.actor.default-dispatcher""
		        connection-string=""data source = localhost:6379;initial catalog={0}""
            }}
        }}";

        public RedisJournalSpec(ITestOutputHelper output) 
            : base(ConfigurationFactory.ParseString(string.Format(ConfigFormat, Counter.IncrementAndGet())), "RedisJournalSpec", output: output)
        {
            TestSetup.Cleanup(RedisPersistence.Get(Sys).JournalSettings);
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            TestSetup.Cleanup(RedisPersistence.Get(Sys).JournalSettings);
        }
    }
}