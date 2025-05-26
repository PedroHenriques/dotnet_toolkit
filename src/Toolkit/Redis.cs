using Toolkit.Types;
using StackExchange.Redis;

namespace Toolkit;

public class Redis : ICache, IQueue
{
  private readonly RedisInputs _inputs;
  private readonly IDatabase _db;

  public Redis(RedisInputs inputs)
  {
    this._inputs = inputs;
    this._db = this._inputs.Client.GetDatabase(0);
  }

  public async Task<string?> GetString(string key)
  {
    RedisValue result = await this._db.StringGetAsync(key);

    if (result.HasValue == false || result.IsNull)
    {
      return null;
    }

    return result.ToString();
  }

  public async Task<Dictionary<string, string>?> GetHash(string key)
  {
    var entries = await this._db.HashGetAllAsync(key);
    if (entries == null || entries.Length == 0) { return null; }

    return entries.ToStringDictionary();
  }

  public Task<bool> Set(string key, string value, TimeSpan? expiry = null)
  {
    return this._db.StringSetAsync(key, value, expiry);
  }

  public async Task<bool> Set(
    string key, Dictionary<string, string> values, TimeSpan? expiry = null
  )
  {
    HashEntry[] entries = values.Select(
      pair => new HashEntry(pair.Key, pair.Value)
    ).ToArray();

    await this._db.HashSetAsync(key, entries);

    if (expiry != null)
    {
      return await this._db.KeyExpireAsync(key, expiry);
    }

    return true;
  }

  public Task<bool> Remove(string key)
  {
    return this._db.KeyDeleteAsync(key);
  }

  public async Task<string[]> Enqueue(string queueName, string[] messages)
  {
    var messageIds = new List<string>();

    foreach (var message in messages)
    {
      var entryId = await _db.StreamAddAsync(queueName, new NameValueEntry[]
      {
        new("data", message),
        new("retries", "0")
      });
      messageIds.Add(entryId);
    }

    return messageIds.ToArray();
  }

  public async Task<(string? id, string? message)> Dequeue(
    string queueName, string consumerName
  )
  {
    try
    {
      await _db.StreamCreateConsumerGroupAsync(
        queueName, this._inputs.ConsumerGroupName, "0", true
      );
    }
    catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP")) { }

    var entries = await _db.StreamReadGroupAsync(
      queueName, this._inputs.ConsumerGroupName, consumerName, ">", 1, noAck: false
    );

    if (entries.Length == 0)
    {
      return (null, null);
    }

    var entry = entries[0];
    return (entry.Id, entry["data"]);
  }

  public async Task<bool> Ack(string queueName, string messageId, bool deleteMessage = true)
  {
    var result = await _db.StreamAcknowledgeAsync(
      queueName, this._inputs.ConsumerGroupName, messageId
    );

    if (deleteMessage && result > 0)
    {
      await _db.StreamDeleteAsync(queueName, [messageId]);
    }

    return result > 0;
  }

  public async Task<bool> Nack(string queueName, string messageId, int retryThreashold)
  {
    var entries = await _db.StreamRangeAsync(queueName, messageId, messageId);

    if (entries.Length == 0) { return false; }

    var entry = entries[0];
    var data = entry["data"];
    var retries = entry.Values.FirstOrDefault(x => x.Name == "retries").Value;

    int retryCount = int.TryParse(retries, out var count) ? count + 1 : 1;

    if (retryCount >= retryThreashold)
    {
      await _db.StreamAddAsync($"{queueName}_dlq", new NameValueEntry[]
      {
        new("data", data),
        new("original_id", entry.Id),
        new("retries", retryCount.ToString())
      });
    }
    else
    {
      await _db.StreamAddAsync(queueName, new NameValueEntry[]
      {
        new("data", data),
        new("retries", retryCount.ToString())
      });
    }

    var _ = Task.Run(async () => await Ack(queueName, messageId, true));

    return true;
  }
}