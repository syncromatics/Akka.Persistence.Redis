﻿//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Pattern;

namespace CustomSerialization.MsgPack
{
    class Program
    {
        // if you want to benchmark your persistent storage provides, paste the configuration in string below
        // by default we're checking against in-memory journal
        private static Config config = ConfigurationFactory.ParseString(@"
            akka {
                suppress-json-serializer-warning = true
                persistence.journal {
                    plugin = ""akka.persistence.journal.redis""
                    redis {
                        class = ""Akka.Persistence.Redis.Journal.RedisJournal, Akka.Persistence.Redis""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
						configuration-string = ""127.0.0.1:6379""
						database = 0
                    }
                }
			}
            akka.actor.serializers.redis-msgpack = ""CustomSerialization.MsgPack.Serialization.MsgPackSerializer, CustomSerialization.MsgPack""
            akka.actor.serialization-bindings.""Akka.Persistence.IPersistentRepresentation, Akka.Persistence"" = redis-msgpack
            akka.actor.serialization-bindings.""CustomSerialization.MsgPack.Stored, CustomSerialization.MsgPack"" = redis-msgpack");

        public const int ActorCount = 1000;
        public const int MessagesPerActor = 100;

        static void Main(string[] args)
        {
            using (var system = ActorSystem.Create("persistent-benchmark", config.WithFallback(ConfigurationFactory.Default())))
            {
                Console.WriteLine("Performance benchmark starting...");

                var actors = new IActorRef[ActorCount];
                for (int i = 0; i < ActorCount; i++)
                {
                    var pid = "a-" + i;
                    actors[i] = system.ActorOf(Props.Create(() => new PerformanceTestActor(pid)));
                }

                Task.WaitAll(actors.Select(a => a.Ask<Done>(Init.Instance)).Cast<Task>().ToArray());

                Console.WriteLine("All actors have been initialized...");

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                for (int i = 0; i < MessagesPerActor; i++)
                {
                    for (int j = 0; j < ActorCount; j++)
                    {
                        actors[j].Tell(new Store(1));
                    }
                }

                var finished = new Task[ActorCount];
                for (int i = 0; i < ActorCount; i++)
                {
                    finished[i] = actors[i].Ask<Finished>(Finish.Instance);
                }

                Task.WaitAll(finished);

                var elapsed = stopwatch.ElapsedMilliseconds;

                Console.WriteLine($"{ActorCount} actors stored {MessagesPerActor} events each in {elapsed/1000.0} sec. Average: {ActorCount*MessagesPerActor*1000.0/elapsed} events/sec");

                foreach (Task<Finished> task in finished)
                {
                    if (!task.IsCompleted || task.Result.State != MessagesPerActor)
                        throw new IllegalStateException("Actor's state was invalid");
                }
            }

            Console.ReadLine();
        }
    }
}
