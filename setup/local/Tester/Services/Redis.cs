using StackExchange.Redis;
using TKRedis = Toolkit.Redis;
using RedisUtils = Toolkit.Utils.Redis;
using Toolkit.Types;
using Newtonsoft.Json;

namespace Tester.Services;

class Redis
{
  public Redis(WebApplication app, MyValue document)
  {
    string? redisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR");
    if (redisConStr == null)
    {
      throw new Exception("Could not get the 'REDIS_CON_STR' environment variable");
    }
    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { redisConStr },
    };
    RedisInputs redisInputs = RedisUtils.PrepareInputs(redisConOpts, "my_tester_consumer_group");
    ICache redis = new TKRedis(redisInputs);
    IQueue redisQueue = new TKRedis(redisInputs);

    app.MapPost("/redis", async () =>
    {
      await redis.Set("prop1", document.Prop1);
      await redis.Set("prop2", document.Prop2, TimeSpan.FromMinutes(5));
      await redis.Set("hashKey", new Dictionary<string, string>() { { "prop1", document.Prop1 }, { "prop2", document.Prop2 } }, TimeSpan.FromMinutes(15));

      return Results.Ok("Keys inserted.");
    });

    app.MapGet("/redis", async () =>
    {
      Console.WriteLine($"Key: prop1 | Value: {await redis.GetString("prop1")}");
      Console.WriteLine($"Key: prop2 | Value: {await redis.GetString("prop2")}");
      Console.WriteLine($"Key: hashKey | Value: {string.Join(Environment.NewLine, await redis.GetHash("hashKey"))}");

      return Results.Ok("Values printed to console.");
    });

    app.MapPost("/redis/queue", async () =>
    {
      string[] ids = await redisQueue.Enqueue("my_queue", new[] { (string)JsonConvert.SerializeObject(document) }, TimeSpan.FromMinutes(5));
      return Results.Ok($"Message enqueued. Inserted IDs: {JsonConvert.SerializeObject(ids)}");
    });

    app.MapGet("/redis/queue", async () =>
    {
      var (id, message) = await redisQueue.Dequeue("my_queue", "tester-1", 1);

      if (String.IsNullOrWhiteSpace(id))
      {
        Console.WriteLine("No messages to process.");
      }
      else
      {
        Console.WriteLine($"id: {id} | message: {message}");
        await redisQueue.Ack("my_queue", id, false);
      }
      return Results.Ok(message);
    });

    app.MapGet("/redis/queue/noAck", async () =>
    {
      var (id, message) = await redisQueue.Dequeue("my_queue", "tester-1", 1);

      if (String.IsNullOrWhiteSpace(id))
      {
        Console.WriteLine("No messages to process.");
      }
      else
      {
        Console.WriteLine($"id: {id} | message: {message}");
        Console.WriteLine("Not Acking the message");
      }
      return Results.Ok(message);
    });
  }
}