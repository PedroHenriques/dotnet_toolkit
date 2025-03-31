using StackExchange.Redis;
using TKRedis = Toolkit.Redis;
using RedisUtils = Toolkit.Utils.Redis;

namespace Tester.Services;

class Redis
{
  public Redis(WebApplication app, dynamic document)
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
    var redisInputs = RedisUtils.PrepareInputs(redisConOpts);
    var redis = new TKRedis(redisInputs);

    app.MapPost("/redis", async () =>
    {
      await redis.Set("prop1", document.prop1);
      await redis.Set("prop2", document.prop2, TimeSpan.FromMinutes(5));
      await redis.Set("hashKey", new Dictionary<string, string>() { { "prop1", document.prop1 }, { "prop2", document.prop2 } }, TimeSpan.FromMinutes(15));

      return Results.Ok("Keys inserted.");
    });

    app.MapGet("/redis", async () =>
    {
      Console.WriteLine($"Key: prop1 | Value: {await redis.GetString("prop1")}");
      Console.WriteLine($"Key: prop2 | Value: {await redis.GetString("prop2")}");
      Console.WriteLine($"Key: hashKey | Value: {string.Join(Environment.NewLine, await redis.GetHash("hashKey"))}");

      return Results.Ok("Values printed to console.");
    });
  }
}