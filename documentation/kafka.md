# Toolkit for Kafka
## How to use
```c#
using Toolkit;
using Toolkit.Types;
// Entity is some data type
using KafkaUtils = Toolkit.Utils.Kafka<string, Entity>;

SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = "schema registry url" };

// If your application is going to produce events
ProducerConfig producerConfig = new ProducerConfig
{
  BootstrapServers = "kafka broker connection string",
};

// If your application is going to consume events
ConsumerConfig consumerConfig = new ConsumerConfig
{
  BootstrapServers = "kafka broker connection string",
  GroupId = "example-consumer-group",
  AutoOffsetReset = AutoOffsetReset.Latest,
  EnableAutoCommit = false,
};

KafkaInputs<string, Entity> kafkaInputs = KafkaUtils.PrepareInputs(
  schemaRegistryConfig, "myTestTopic-value", 1, producerConfig, consumerConfig
);
IKafka<string, Entity> kafka = new Kafka<string, Entity>(kafkaInputs);
```

In the above snippet we:
- Start by using the `KafkaUtils.PrepareInputs()` utility function to let the Toolkit handle all the necessary setup for interactions with Kafka.
- Then we instantiate the Toolkit's Kafka class

The instance of `IKafka` exposes the following functionality:

```c#
public interface IKafka<TKey, TValue>
{
  public void Publish(
    string topicName, Message<TKey, TValue> message,
    Action<DeliveryResult<TKey, TValue>> handler
  );

  public void Subscribe(
    IEnumerable<string> topics,
    Action<ConsumeResult<TKey, TValue>> handler,
    CancellationTokenSource? consumerCTS
  );

  public void Commit(ConsumeResult<TKey, TValue> consumeResult);
}
```

### Publish
Publishes the provided `message` event in the provided `topicName` topic, asynchronously.<br>
Once the message has been published, the provided `handler` callback will be invoked with information about the published message.<br>
**NOTE:** Requires that a `ProducerConfig` was provided to `KafkaUtils.PrepareInputs()`.<br><br>
Throws Exceptions (generic and Kafka specific) on error.

**Example use**
```c#
// Entity is some data type
Entity myEntity = new Entity {
  Name = "test name",
  Hello = "world",
};

kafka.Publish(
  "myTestTopic",
  new Message<string, Entity> { Key = DateTime.UtcNow.ToString(), Value = myEntity },
  (res) => { Console.WriteLine($"Event inserted in partition: {res.Partition} and offset: {res.Offset}."); }
);
```

### Subscribe
Subscribes to the provided `topics` topics.<br>
When an event is published in one of the topics, the provided `handler` callback will be invoked with information about the event.<br>
To stop subscribing to these topics, `Cancel()` the provided `CancellationTokenSource`.<br>
**NOTE:** Requires that a `ConsumerConfig` was provided to `KafkaUtils.PrepareInputs()`.<br><br>
Throws Exceptions (generic and Kafka specific) on error.

**Example use**
```c#
CancellationTokenSource cts = new CancellationTokenSource();
kafka.Subscribe(
  ["myTestTopic"],
  (res) =>
  {
    Console.WriteLine($"Processing event from partition: {res.Partition} | offset: {res.Offset}");
    Console.WriteLine(res.Message.Value);
  },
  cts
);

// To stop subscribing
cts.Cancel();
```

### Commit
Commits the offset of the provided `consumeResult` consumed event, for the consumer group registered during setup.<br><br>
**NOTE:** Requires that a `ConsumerConfig` was provided to `KafkaUtils.PrepareInputs()`.<br><br>
Throws Exceptions (generic and Kafka specific) on error.

**Example use**
```c#
CancellationTokenSource cts = new CancellationTokenSource();
kafka.Subscribe(
  ["myTestTopic"],
  (res) =>
  {
    // Do something with the received event
    
    kafka.Commit(res);
  },
  cts
);
```