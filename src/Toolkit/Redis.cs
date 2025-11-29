using Toolkit.Types;
using StackExchange.Redis;
using System.Diagnostics;

namespace Toolkit;

public class Redis : ICache, IQueue
{
  private const string _traceIdPropName = "traceId";
  private const string _parkingConsumerName = "parkingConsumer";
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

  public async Task<string[]> Enqueue(
    string queueName, string[] messages, TimeSpan? ttl = null
  )
  {
    var messageIds = new List<string>();

    for (int i = 0; i < messages.Length; i++)
    {
      var entryId = await AddToStream(
        queueName, messages[i], null, null, i == 0 ? ttl : null
      );
      messageIds.Add(entryId ?? "N/A");
    }

    return messageIds.ToArray();
  }

  public async Task<(string? id, string? message)> Dequeue(
    string queueName, string consumerName, double visibilityTimeoutMin = 5
  )
  {
    try
    {
      await this._db.StreamCreateConsumerGroupAsync(
        queueName, this._inputs.ConsumerGroupName, "0", true
      );
    }
    catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP")) { }

    // Not unit testable due to the return of StreamAutoClaimAsync - StreamAutoClaimResult - being read only, so not mockable
    RedisValue startId = "0-0";
    while (true)
    {
      var claimed = await this._db.StreamAutoClaimAsync(
        queueName, this._inputs.ConsumerGroupName, consumerName,
        (long)(visibilityTimeoutMin * 60 * 1000), startId, 1
      );

      if (claimed.ClaimedEntries.Length > 0)
      {
        var claimedEntry = claimed.ClaimedEntries[0];
        return (claimedEntry.Id, claimedEntry["data"]);
      }

      if (claimed.NextStartId.IsNullOrEmpty || claimed.NextStartId == startId)
      {
        break;
      }

      startId = claimed.NextStartId;
    }
    // End of not unit testable block

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

  public async Task<bool> Nack(
    string queueName, string messageId, int retryThreshold, string consumerName
  )
  {
    var entries = await this._db.StreamRangeAsync(queueName, messageId, messageId);

    if (entries.Length == 0)
    {
      throw new Exception($"No message found with the provided id: '{messageId}'");
    }

    var pendingMsgInfo = await this._db.StreamPendingMessagesAsync(
      queueName, this._inputs.ConsumerGroupName, 1, consumerName, messageId,
      messageId, CommandFlags.None
    );

    if (pendingMsgInfo.Length == 0)
    {
      throw new Exception($"Message '{messageId}' is not pending for group '{this._inputs.ConsumerGroupName}'");
    }

    int retryCount = pendingMsgInfo[0].DeliveryCount;

    if (retryCount >= retryThreshold)
    {
      var entry = entries[0];

      string originalId = entry.Id.ToString();
      if (entry["original_id"].IsNullOrEmpty == false)
      {
        originalId = $"{entry["original_id"]} | {originalId}";
      }

      await AddToStream(
        $"{queueName}_dlq", entry["data"].ToString(), retryCount,
        [new("original_id", originalId)]
      );
      await Ack(queueName, messageId, true);
      return false;
    }

    await this._db.ExecuteAsync("XCLAIM", new object[]
    {
      queueName,
      this._inputs.ConsumerGroupName,
      _parkingConsumerName,
      0, // min-idle-ms (0 means "claim regardless of idle")
      messageId,
      "IDLE", 0, // reset idle so visibility timeout starts now
      "RETRYCOUNT", retryCount,
      "JUSTID"
    });

    return true;
  }

  private async Task<string?> AddToStream(
    string queueName, string data, int? retryCount, NameValueEntry[]? extraData = null,
    TimeSpan? ttl = null
  )
  {
    var args = new List<object> { queueName };

    if (ttl != null)
    {
      long cutoffMs = DateTimeOffset.UtcNow.Subtract((TimeSpan)ttl).ToUnixTimeMilliseconds();
      args.Add("MINID");
      args.Add($"{cutoffMs}-0");
    }

    args.Add("*");
    args.Add("data");
    args.Add(data);

    if (Activity.Current != null)
    {
      args.Add(_traceIdPropName);
      args.Add(Activity.Current.TraceId.ToString());
    }

    if (retryCount != null)
    {
      args.Add("retries");
      args.Add(retryCount.ToString());
    }

    if (extraData != null)
    {
      foreach (var ed in extraData)
      {
        args.Add(ed.Name);
        args.Add(ed.Value);
      }
    }

    return (string?)await this._db.ExecuteAsync("XADD", args.ToArray());
  }
}