using StackExchange.Redis;

namespace Toolkit.Types;

public struct RedisInputs
{
  public required IConnectionMultiplexer Client { get; set; }
  public required string ConsumerGroupName { get; set; }
  public IFeatureFlags? FeatureFlags { get; set; }
  public ILogger? Logger { get; set; }
  public string? ActivitySourceName { get; set; }
  public string? ActivityName { get; set; }
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
  public Task<string[]> Enqueue(
    string queueName, string[] messages, TimeSpan? ttl = null
  );

  public Task<T?> Dequeue<T>(
    string queueName, string consumerName, Func<(string?, string?), Task<T?>> handler,
    double visibilityTimeoutMin = 5
  );

  public void Subscribe(
    string queueName, string consumerName,
    Func<(string?, string?), Exception?, Task> handler,
    CancellationToken consumerCT, double visibilityTimeoutMin = 5,
    double pollingDelaySec = 0
  );

  public void Subscribe(
    string queueName, string consumerName,
    Func<(string?, string?), Exception?, Task> handler,
    string featureFlagKey, double visibilityTimeoutMin = 5,
    double pollingDelaySec = 0
  );

  public Task<bool> Ack(string queueName, string messageId, bool deleteMessage = true);

  public Task<bool> Nack(
    string queueName, string messageId, int retryThreshold, string consumerName
  );
}