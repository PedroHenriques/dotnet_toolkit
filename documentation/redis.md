# Toolkit for Redis
## How to use
```c#
using Toolkit;
using Toolkit.Types;
using RedisUtils = Toolkit.Utils.Redis;

ConfigurationOptions redisConOpts = new ConfigurationOptions
{
  EndPoints = { "my connection string" },
};
RedisInputs redisInputs = RedisUtils.PrepareInputs(redisConOpts);
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
  public Task<long> Enqueue(string queueName, string[] messages);

  public Task<string> Dequeue(string queueName);

  public Task<bool> Ack(string queueName, string message);

  public Task<bool> Nack(string queueName, string message);
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
Returns the number of messages in the queue, after the enqueue operation.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Enqueue("queue name", [ "message 1", "message 2" ]);
```

### IQueue - Dequeue
Moves the first value from the provided `queueName` queue to the "in process" queue, asynchronously, and returns a copy of that message.<br>
The name of the queue containing the messages that are "in process" is `queueName` with the suffix `_temp`<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Dequeue("queue name", [ "message 1", "message 2" ]);
```

### IQueue - Ack
Acknowledges that the provided `message` value, from the provided `queueName` queue, has been processed.<br>
This will delete the processed message from the "in process" queue.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Ack("queue name", "message 1");
```

### IQueue - Nack
Signals that the provided `message` value, from the provided `queueName` queue, has not been processed.<br>
This will delete the processed message from the "in process" queue.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Nack("queue name", "message 1");
```