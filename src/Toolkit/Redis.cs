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

  public Task<long> Enqueue(string queueName, string[] messages)
  {
    RedisValue[] values = messages.Select(message => (RedisValue)message)
      .ToArray();

    return this._db.ListLeftPushAsync(queueName, values, CommandFlags.None);
  }

  public async Task<string> Dequeue(string queueName)
  {
    var item = await this._db.ListMoveAsync(queueName, $"{queueName}_temp",
      ListSide.Right, ListSide.Left);

    return item.ToString();
  }

  public async Task<bool> Ack(string queueName, string message)
  {
    return await this._db.ListRemoveAsync($"{queueName}_temp", message, 0) > 0;
  }

  public Task<bool> Nack(string queueName, string message)
  {
    // @TODO: Log it

    return Ack(queueName, message);
  }
}