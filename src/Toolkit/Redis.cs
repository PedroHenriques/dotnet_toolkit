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
      var entryId = await AddToStream(queueName, message, 0);
      messageIds.Add(entryId ?? "N/A");
    }

    return messageIds.ToArray();
  }

  public async Task<(string? id, string? message)> Dequeue(
    string queueName, string consumerName
  )
  {
    try
    {
      await this._db.StreamCreateConsumerGroupAsync(
        queueName, this._inputs.ConsumerGroupName, "0", true
      );
    }
    catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP")) { }

    var entries = await this._db.StreamReadGroupAsync(
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
    var result = await this._db.StreamAcknowledgeAsync(
      queueName, this._inputs.ConsumerGroupName, messageId
    );

    if (deleteMessage && result > 0)
    {
      await this._db.StreamDeleteAsync(queueName, [messageId]);
    }

    return result > 0;
  }

  public async Task<bool> Nack(string queueName, string messageId, int retryThreshold)
  {
    var entries = await this._db.StreamRangeAsync(queueName, messageId, messageId);

    if (entries.Length == 0)
    {
      throw new Exception($"No message found with the provided id: '{messageId}'");
    }

    var entry = entries[0];
    var data = entry["data"].ToString();

    var retries = entry.Values.FirstOrDefault(x => x.Name == "retries").Value;
    int retryCount = int.TryParse(retries, out var count) ? count + 1 : 1;

    bool returnValue = true;
    if (retryCount >= retryThreshold)
    {
      await AddToStream(
        $"{queueName}_dlq", data, retryCount, [new("original_id", entry.Id)]
      );
      returnValue = false;
    }
    else
    {
      await AddToStream(queueName, data, retryCount);
    }

    var _ = Task.Run(async () => await Ack(queueName, messageId, true));

    return returnValue;
  }

  private async Task<string?> AddToStream(
    string queueName, string data, int retryCount, NameValueEntry[]? extraData = null
  )
  {
    var content = new NameValueEntry[]
    {
      new("data", data),
      new("retries", retryCount.ToString()),
    };

    if (extraData != null)
    {
      content = content.Concat(extraData).ToArray();
    }

    return await this._db.StreamAddAsync(queueName, content);
  }
}