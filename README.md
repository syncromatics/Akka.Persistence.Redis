# Akka.Persistence.Redis [![Build status](https://ci.appveyor.com/api/projects/status/muvegsad9hu26ovf/branch/master?svg=true)](https://ci.appveyor.com/project/ravengerUA/akka-persistence-stackexchangeredis/branch/master)

Akka Persistence journal and snapshot store backed by Redis database.
Based on https://github.com/StackExchange/StackExchange.Redis library.

### Configuration

Both journal and snapshot store share the same configuration keys (however they resides in separate scopes, so they are defined distinctly for either journal or snapshot store):

Remember that connection string must be provided separately to Journal and Snapshot Store.

```hocon
akka.persistence {
    journal {
		plugin = "akka.persistence.journal.redis"
        redis {
            # qualified type name of the Redis persistence journal actor
            class = "Akka.Persistence.Redis.Journal.RedisJournal, Akka.Persistence.Redis"

            #connection string, as described here: https://github.com/StackExchange/StackExchange.Redis/blob/master/Docs/Configuration.md#basic-configuration-strings
            configuration-string = "127.0.0.1:6379"

            # dispatcher used to drive journal actor
            plugin-dispatcher = "akka.actor.default-dispatcher"

            #Redis journals key prefixes. Leave it for default or change it to appropriate value. WARNING: don't change it on production instances.
            key-prefix = "akka:persistence:journal"
        }
    }    

    snapshot-store {
		plugin = "akka.persistence.snapshot-store.redis"
        redis {
            # qualified type name of the Redis persistence snapshot storage actor
            class = "Akka.Persistence.Redis.Snapshot.RedisSnapshotStore, Akka.Persistence.Redis"

            #connection string, as described here: https://github.com/StackExchange/StackExchange.Redis/blob/master/Docs/Configuration.md#basic-configuration-strings
            configuration-string = "127.0.0.1:6379"

            # dispatcher used to drive snapshot storage actor
            plugin-dispatcher = "akka.actor.default-dispatcher"

            #Redis storage key prefixes. Leave it for default or change it to appropriate value. WARNING: don't change it on production instances.
            key-prefix = "akka:persistence:snapshots"
        }
    }
}
```
