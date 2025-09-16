using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using DbFixtures.Kafka;
using Newtonsoft.Json;
using Toolkit.Types;
using KafkaUtils = Toolkit.Utils.Kafka<Toolkit.Tests.Integration.TestKey, Toolkit.Tests.Integration.TestValue>;

namespace Toolkit.Tests.Integration;

[Trait("Type", "Integration")]
public class KafkaTests : IDisposable, IAsyncLifetime
{
  private const string TOPIC_NAME = "testTopic";
  private readonly IAdminClient _adminClient;
  private readonly IConsumer<TestKey, TestValue> _realConsumer;
  private readonly DbFixtures.DbFixtures _dbFixtures;
  private readonly IKafka<TestKey, TestValue> _sut;

  public KafkaTests()
  {
    this._adminClient = new AdminClientBuilder(
      new AdminClientConfig { BootstrapServers = "broker:29092" }
    ).Build();

    SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://schema-registry:8081" };
    ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

    var consumer = new ConsumerBuilder<Ignore, Ignore>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "cleanup-group",
        AutoOffsetReset = AutoOffsetReset.Latest
      }
    ).Build();
    this._realConsumer = new ConsumerBuilder<TestKey, TestValue>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    )
    .SetKeyDeserializer(new JsonDeserializer<TestKey>(schemaRegistry).AsSyncOverAsync())
    .SetValueDeserializer(new JsonDeserializer<TestValue>(schemaRegistry).AsSyncOverAsync())
    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
    .Build();

    var jsonSerializerConfig = new JsonSerializerConfig
    {
      AutoRegisterSchemas = true,
    };
    var producer = new ProducerBuilder<TestKey, TestValue>(
      new ProducerConfig
      {
        BootstrapServers = "broker:29092",
      }
    )
    .SetKeySerializer(new JsonSerializer<TestKey>(schemaRegistry, jsonSerializerConfig))
    .SetValueSerializer(new JsonSerializer<TestValue>(schemaRegistry, jsonSerializerConfig))
    .Build();

    var driver = new KafkaDriver<TestKey, TestValue>(this._adminClient, consumer, producer);

    this._dbFixtures = new DbFixtures.DbFixtures([driver]);

    this._sut = new Kafka<TestKey, TestValue>(
      KafkaUtils.PrepareInputs(
        schemaRegistryConfig,
        new ProducerConfig
        {
          BootstrapServers = "broker:29092",
        },
        new ConsumerConfig
        {
          BootstrapServers = "broker:29092",
          GroupId = "example-consumer-group",
          AutoOffsetReset = AutoOffsetReset.Earliest,
          EnableAutoCommit = false,
        }
      )
    );
  }

  public void Dispose()
  {
    this._dbFixtures.CloseDrivers();
    this._realConsumer.Close();
    this._realConsumer.Dispose();
  }

  public Task DisposeAsync()
  {
    return Task.CompletedTask;
  }

  public async Task InitializeAsync()
  {
    try
    {
      await _adminClient.CreateTopicsAsync(new[]
      {
        new TopicSpecification { Name = TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 },
      });
    }
    catch (CreateTopicsException ex)
    {
      if (ex.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists)) { throw; }
    }
  }

  [Fact]
  public async Task Publish_ItShouldInsertEventInTopic()
  {
    Message<TestKey, TestValue>[] expectedMessages = [
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "seed id 1" },
        Value = new TestValue { Name = "seed name 1" },
      },
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "test id" },
        Value = new TestValue { Name = "test name" },
      }
    ];

    await this._dbFixtures.InsertFixtures<Message<TestKey, TestValue>>(
      [TOPIC_NAME],
      new Dictionary<string, Message<TestKey, TestValue>[]>
      {
        { TOPIC_NAME, [ expectedMessages[0] ] },
      }
    );

    var cts = new CancellationTokenSource(5000);

    DeliveryResult<TestKey, TestValue>? delRes = null;
    Exception? exception = null;

    this._sut.Publish(
      "testTopic",
      expectedMessages[1],
      (res, ex) =>
      {
        delRes = res;
        exception = ex;
        cts.Cancel();
      }
    );

    try
    {
      await Task.Delay(-1, cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    { }

    Assert.Null(exception);
    Assert.NotNull(delRes);

    this._realConsumer.Subscribe(TOPIC_NAME);
    List<Message<TestKey, TestValue>> records = new List<Message<TestKey, TestValue>> { };
    while (records.Count < expectedMessages.Length)
    {
      var cr = _realConsumer.Consume(TimeSpan.FromSeconds(1));
      if (cr != null)
      {
        var msg = cr.Message;
        msg.Timestamp = Timestamp.Default;
        msg.Headers = null;
        records.Add(msg);
      }
    }

    Assert.Equal(
      JsonConvert.SerializeObject(expectedMessages),
      JsonConvert.SerializeObject(records)
    );
  }

  [Fact]
  public async Task Subscribe_WithCTS_ItShouldConsumeEventsFromTopic()
  {
    Message<TestKey, TestValue>[] expectedMessages = [
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "seed id 1" },
        Value = new TestValue { Name = "seed name 1" },
      },
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "test id" },
        Value = new TestValue { Name = "test name" },
      }
    ];

    await this._dbFixtures.InsertFixtures<Message<TestKey, TestValue>>(
      [TOPIC_NAME],
      new Dictionary<string, Message<TestKey, TestValue>[]>
      {
        { TOPIC_NAME, expectedMessages },
      }
    );

    List<Message<TestKey, TestValue>> records = new List<Message<TestKey, TestValue>> { };
    Exception[] exceptions = [];
    var cts = new CancellationTokenSource(5000);
    this._sut.Subscribe(
      [TOPIC_NAME],
      (res, ex) =>
      {
        if (ex != null)
        {
          exceptions.Append(ex);
        }

        if (res != null)
        {
          var msg = res.Message;
          msg.Timestamp = Timestamp.Default;
          msg.Headers = null;
          records.Add(msg);

          this._sut.Commit(res);
        }

        if (records.Count == expectedMessages.Length)
        {
          cts.Cancel();
        }
      },
      cts
    );

    try
    {
      await Task.Delay(-1, cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    { }

    Assert.Empty(exceptions);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedMessages),
      JsonConvert.SerializeObject(records)
    );
  }
}

public class TestKey
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }
}

public class TestValue
{
  [JsonPropertyName("name")]
  [JsonProperty("name")]
  public required string Name { get; set; }
}