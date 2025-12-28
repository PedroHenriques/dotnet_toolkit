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
Returns the IDs of enqueued messages.<br>
**NOTE:** If a `ttl` argument is provided, then all messages older than the provided TTL will be deleted.<br>
**NOTE:** If there is an active Activity, then the current Trace ID will be stored with the enqueued message.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Enqueue("queue name", [ "message 1", "message 2" ]);
```

### IQueue - Dequeue
Checks if there is any "parked" message for longer than `visibilityTimeoutMin` minutes. If there is then reclaims that message for the specified `consumerName` consumer name (in the consumer group name provided to `PrepareInputs`), and returns a copy of that message.<br>
Otherwise, reserves the first message in the provided `queueName` queue, for the specified `consumerName` consumer name (in the consumer group name provided to `PrepareInputs`), and returns a copy of that message.<br>
Will give preference to reclaimed messages.<br>
Waits for the callback to resolve and returns its value.<br>
**NOTE:** If the message has a Trace ID, then it will be set as the current Activity's Trace ID.<br>
If the Trace ID extracted from the message is not a valid trace ID, then a random one will be generated and a warning log will be generated.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
app.MapGet("/redis/queue", async () =>
{
  return await redis.Dequeue<IResult>(
    "queue name", "my consumer name 1",
    async (message) =>
    {
      var (id, msg) = message;

      if (String.IsNullOrWhiteSpace(id))
      {
        logger.Log(LogLevel.Information, null, "No messages to process.");
      }
      else
      {
        logger.Log(LogLevel.Information, null, $"id: {id} | message: {msg}");
        await redisQueue.Ack("queue name", id, false);
      }
      return Results.Ok(msg);
    },
    2
  );
});
```

### Subscribe (with cancellation token)
Subscribes to the provided `queueName` stream.<br>
Performs a long poll, with `pollingDelaySec` second interval, to the stream using the method `Dequeue`, so consult that methods documentation for more details.<br>
Will wait for the callback to resolve and then wait `pollingDelaySec` seconds before fetching a new message.<br>
**NOTE:** Your application has 2 direct ways to control the cadence of pulling messages from the queue: `when you return from the callback` and `the pollingDelaySec argument`. Using a combination of these 2 levers you have full control over the throughput of your aplication.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
var cts = new CancellationTokenSource();
redis.Subscribe(
  "testStream", "some rng group",
  async (message, ex) =>
  {
    var (id, msg) = message;

    if (ex != null) { logger.Log(LogLevel.Error, ex, $"Redis.Subscribe: {ex.Message}"); }

    if (String.IsNullOrWhiteSpace(id))
    {
      logger.Log(LogLevel.Information, null, "Redis.Subscribe: No messages to process.");
      return;
    }

    logger.Log(LogLevel.Information, null, $"Redis.Subscribe: id: {id} | message: {msg}");
    await redis.Ack("testStream", id, false);
  },
  cts.Token, 1, 0.5
);
```

To stop receiving messages from the Redis stream, cancel the cancellation token.
```c#
cts.Cancel();
```

### Subscribe (with feature flag)
Subscribes to the provided `queueName` stream, if the provided feature flag is `true`.<br>
Performs a long poll, with `pollingDelaySec` second interval, to the stream using the method `Dequeue`, so consult that methods documentation for more details.<br>
Will wait for the callback to resolve and then wait `pollingDelaySec` seconds before fetching a new message.<br>
**NOTE:** Your application has 2 direct ways to control the cadence of pulling messages from the queue: `when you return from the callback` and `the pollingDelaySec argument`. Using a combination of these 2 levers you have full control over the throughput of your aplication.<br>
**NOTE:** Requires that an `IFeatureFlags` instance was provided to `RedisUtils.PrepareInputs()`.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
redis.Subscribe(
  "testStream", "some rng group",
  async (message, ex) =>
  {
    var (id, msg) = message;

    if (ex != null) { logger.Log(LogLevel.Error, ex, $"Redis.Subscribe: {ex.Message}"); }

    if (String.IsNullOrWhiteSpace(id))
    {
      logger.Log(LogLevel.Information, null, "Redis.Subscribe: No messages to process.");
      return;
    }

    logger.Log(LogLevel.Information, null, $"Redis.Subscribe: id: {id} | message: {msg}");
    await redis.Ack("testStream", id, false);
  },
  "a feature flag key", 1, 0.5
);
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
If the message has not reached the provided `retryThreshold` argument, then the message will be parked in a consumer named `parkingConsumer`, until the message is reclaimed with `Dequeue()`.<br><br>
Returns `true` if the message was requeued.<br>
Returns `false` if the message was sent to the dlq.<br><br>
Throws Exceptions (generic and Redis specific) on error.

**Example use**
```c#
await redis.Nack("queue name", "id of the message", 5, "my consumer name 1");
```
