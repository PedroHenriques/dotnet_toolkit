using StackExchange.Redis;

namespace Toolkit.Types;

public struct RedisInputs
{
  public required IConnectionMultiplexer Client { get; set; }
}

public interface ICache
{
  public Task<string?> GetString(string key);

  public Task<Dictionary<string, string>?> GetHash(string key);

  public Task<bool> Set(string key, string value, TimeSpan? expiry = null);

  public Task<bool> Set(string key, Dictionary<string, string> value, TimeSpan? expiry = null);

  public Task<bool> Remove(string key);
}

public interface IQueue
{
  public Task<long> Enqueue(string queueName, string[] messages);

  public Task<string> Dequeue(string queueName);

  public Task<bool> Ack(string queueName, string message);

  public Task<bool> Nack(string queueName, string message);
}