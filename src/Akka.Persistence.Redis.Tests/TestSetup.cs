using System;
using RedisBoost;

namespace Akka.Persistence.Redis.Tests
{
    public static class TestSetup
    {
        internal static void Cleanup(RedisSettings settings)
        {
            var client = RedisClient.ConnectAsync(settings.ConnectionString).Result;
            client.FlushDbAsync().Wait(TimeSpan.FromSeconds(1));
        }
    }
}