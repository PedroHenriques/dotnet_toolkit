# Toolkit for Redis

## Package
This service is included in the `PJHToolkit` package.

## How to use
```c#
using Toolkit;
using Toolkit.Types;
using RedisUtils = Toolkit.Utils.Redis;

ConfigurationOptions redisConOpts = new ConfigurationOptions
{
  EndPoints = { "my connection string" },
};
RedisInputs redisInputs = RedisUtils.PrepareInputs(redisConOpts, "my consumer group name");
ICache redis = new Redis(redisInputs); // Instance for caching
IQueue redis = new Redis(redisInputs); // Instance for queue
```

In the above snippet we:
- Start by using the `RedisUtils.PrepareInputs()` utility function to let the Toolkit handle all the necessary setup for interactions with Redis.
- Then we instantiate the Toolkit's Redis class

The instance of `ICache` exposes the following functionality:

```c#
public interface ICache
{
  public Task<string?> GetString(string key);

  public Task<Dictionary<string, string>?> GetHash(string key);

  public Task<bool> Set(string key, string value, TimeSpan? expiry = null);

  public Task<bool> Set(string key, Dictionary<string, string> value, TimeSpan? expiry = null);

  public Task<bool> Remove(string key);
}
```
The instance of `IQueue` exposes the following functionality:

```c#
public interface IQueue
{
  public Task<string[]> Enqueue(string queueName, string[] messages);

  public Task<(string? id, string? message)> Dequeue(string queueName, string consumerName);

  public Task<bool> Ack(string queueName, string messageId, bool deleteMessage = true);

  public Task<bool> Nack(string queueName, string messageId, int retryThreshold);
}
```

### ICache - GetString
Queries the provided `key` key asynchronously.<br>
**NOTE:** Use this method for key with `string` values.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.GetString("some key");
```

### ICache - GetHash
Queries the provided `key` key asynchronously.<br>
**NOTE:** Use this method for key with `hash` values.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.GetHash("some key");
```

### ICache - Set
Sets the provided `key` key to the provided `value` asynchronously.<br>
If the `expiry` argument is provided, then the key will be set with that TTL.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
// Sets a key to a string value
await redis.Set("some key", "a string value");

// Sets a key to a hash value with a TTL of 5 minutes
Dictionary<string, string> data = new Dictionary<string, string> {
  { "prop 1", "value 1" },
  { "prop 2", "value 2" },
};
await redis.Set("some key", data, TimeSpan.FromMinutes(5));
```

### ICache - Remove
Removes the provided `key` key asynchronously.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Remove("some key");
```

### IQueue - Enqueue
Adds the provided `messages` values to the head of the provided `queueName` queue asynchronously.<br>
Returns the IDs of enqueued messages.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Enqueue("queue name", [ "message 1", "message 2" ]);
```

### IQueue - Dequeue
Reserves the first message in the provided `queueName` queue, for the specified `consumerName` consumer name (in the consumer group name provided to `PrepareInputs`), and returns a copy of that message.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Dequeue("queue name", "my consumer name 1");
```

### IQueue - Ack
Acknowledges that the provided `messageId`, in the provided `queueName` queue, has been processed.<br>
If the `deleteMessage` argument is set to `true`, then the message will be deleted from the queue.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Ack("queue name", "id of the message", true);
```

### IQueue - Nack
Signals that the provided `messageId`, in the provided `queueName` queue, has not been processed.<br><br>
If the message has been processed the same amount of times as the provided `retryThreshold` argument, then the message will be moved to a dead letter queue (dlq). This dlq has the name of the originating queue with the suffix `_dlq`<br><br>
If the message has not reached the provided `retryThreshold` argument, then the message will be enqueued again in the queue.<br><br>
Returns `true` if the message was requeued.<br>
Returns `false` if the message was sent to the dlq.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Nack("queue name", "id of the message", 5);
```