using StackExchange.Redis;
using TKRedis = Toolkit.Redis;
using RedisUtils = Toolkit.Utils.Redis;
using Toolkit.Types;
using Newtonsoft.Json;

namespace Tester.Services;

class Redis
{
  public Redis(WebApplication app, MyValue document, Toolkit.Types.ILogger logger)
  {
    string? redisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR");
    if (redisConStr == null)
    {
      throw new Exception("Could not get the 'REDIS_CON_STR' environment variable");
    }
    string? redisPw = Environment.GetEnvironmentVariable("REDIS_PW");
    if (redisPw == null)
    {
      throw new Exception("Could not get the 'REDIS_PW' environment variable");
    }
    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { redisConStr },
      Password = redisPw,
    };
    RedisInputs redisInputs = RedisUtils.PrepareInputs(
      redisConOpts, "my_tester_consumer_group", logger
    );
    ICache redis = new TKRedis(redisInputs);
    IQueue redisQueue = new TKRedis(redisInputs);
    RedisInputs redisInputsSubscribe = RedisUtils.PrepareInputs(
      redisConOpts, "my_tester_consumer_group_subscribe", logger
    );
    IQueue redisQueueSubscribe = new TKRedis(redisInputsSubscribe);

    app.MapPost("/redis", async () =>
    {
      logger.Log(LogLevel.Information, null, "Started processing request for POST /redis");

      await redis.Set("prop1", document.Prop1);
      await redis.Set("prop2", document.Prop2, TimeSpan.FromMinutes(5));
      await redis.Set("hashKey", new Dictionary<string, string>() { { "prop1", document.Prop1 }, { "prop2", document.Prop2 } }, TimeSpan.FromMinutes(15));

      logger.Log(LogLevel.Information, null, "Inserted keys into Redis");

      return Results.Ok("Keys inserted.");
    });

    app.MapGet("/redis", async () =>
    {
      logger.Log(LogLevel.Information, null, $"Key: prop1 | Value: {await redis.GetString("prop1")}");
      logger.Log(LogLevel.Information, null, $"Key: prop2 | Value: {await redis.GetString("prop2")}");
      logger.Log(LogLevel.Information, null, $"Key: hashKey | Value: {string.Join(Environment.NewLine, await redis.GetHash("hashKey"))}");

      return Results.Ok("Values printed to console.");
    });

    app.MapPost("/redis/queue", async () =>
    {
      string[] ids = await redisQueue.Enqueue("my_queue", new[] { (string)JsonConvert.SerializeObject(document) }, TimeSpan.FromMinutes(5));
      return Results.Ok($"Message enqueued. Inserted IDs: {JsonConvert.SerializeObject(ids)}");
    });

    var cts = new CancellationTokenSource();
    redisQueueSubscribe.Subscribe(
      "my_queue", "tester-subscribe-1",
      async (message) =>
      {
        var (id, msg, ex) = message;

        if (ex != null) { logger.Log(LogLevel.Error, ex, $"Redis.Subscribe: {ex.Message}"); }

        if (String.IsNullOrWhiteSpace(id))
        {
          logger.Log(LogLevel.Information, null, "Redis.Subscribe: No messages to process.");
          return;
        }

        logger.Log(LogLevel.Information, null, $"Redis.Subscribe: id: {id} | message: {msg}");
        await redisQueueSubscribe.Ack("my_queue", id, false);
      },
      cts.Token, 1, 0.5
    );

    app.MapGet("/redis/queue", async () =>
    {
      var (id, message) = await redisQueue.Dequeue("my_queue", "tester-1", 1);

      if (String.IsNullOrWhiteSpace(id))
      {
        logger.Log(LogLevel.Information, null, "No messages to process.");
      }
      else
      {
        logger.Log(LogLevel.Information, null, $"id: {id} | message: {message}");
        await redisQueue.Ack("my_queue", id, false);
      }
      return Results.Ok(message);
    });

    app.MapGet("/redis/queue/nack", async () =>
    {
      var (id, message) = await redisQueue.Dequeue("my_queue", "tester-1", 1);

      if (String.IsNullOrWhiteSpace(id))
      {
        logger.Log(LogLevel.Information, null, "No messages to process.");
      }
      else
      {
        logger.Log(LogLevel.Information, null, $"id: {id} | message: {message}");
        var isRetry = await redisQueue.Nack("my_queue", id, 3, "tester-1");
        if (isRetry)
        {
          logger.Log(LogLevel.Information, null, "Going to retry message");
        }
        else
        {
          logger.Log(LogLevel.Information, null, "Message sent to DLQ");
        }
      }
      return Results.Ok(message);
    });
  }
}