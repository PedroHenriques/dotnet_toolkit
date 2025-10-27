using StackExchange.Redis;

namespace Toolkit.Types;

public struct RedisInputs
{
  public required IConnectionMultiplexer Client { get; set; }

  public required string ConsumerGroupName { get; set; }
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
  public Task<string[]> Enqueue(string queueName, string[] messages, TimeSpan? ttl = null);

  public Task<(string? id, string? message)> Dequeue(
    string queueName, string consumerName, double visibilityTimeoutMin = 5
  );

  public Task<bool> Ack(string queueName, string messageId, bool deleteMessage = true);

  public Task<bool> Nack(
    string queueName, string messageId, int retryThreshold, string consumerName
  );
}