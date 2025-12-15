using System.Text.Json.Serialization;
using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using DbFixtures.Kafka;
using Newtonsoft.Json;
using Toolkit.Types;
using KafkaUtilsJson = Toolkit.Utils.Kafka<Toolkit.Tests.Integration.TestKey, Toolkit.Tests.Integration.TestValue>;
using KafkaUtilsAvro = Toolkit.Utils.Kafka<Avro.Generic.GenericRecord, Avro.Generic.GenericRecord>;

namespace Toolkit.Tests.Integration;

[Trait("Type", "Integration")]
public class KafkaTests : IDisposable, IAsyncLifetime
{
  private const string JSON_TOPIC_NAME = "testTopicJson";
  private const string AVRO_TOPIC_NAME = "testTopicAvro";
  private readonly IAdminClient _adminClient;
  private readonly IConsumer<TestKey, TestValue> _realConsumerJson;
  private readonly IConsumer<GenericRecord, GenericRecord> _realConsumerAvro;
  private readonly DbFixtures.DbFixtures _dbFixturesJson;
  private readonly DbFixtures.DbFixtures _dbFixturesAvro;
  private readonly IKafka<TestKey, TestValue> _sutJson;
  private readonly IKafka<GenericRecord, GenericRecord> _sutAvro;

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
    this._realConsumerJson = new ConsumerBuilder<TestKey, TestValue>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group-json",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    )
    .SetKeyDeserializer(new JsonDeserializer<TestKey>(schemaRegistry).AsSyncOverAsync())
    .SetValueDeserializer(new JsonDeserializer<TestValue>(schemaRegistry).AsSyncOverAsync())
    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
    .Build();
    this._realConsumerAvro = new ConsumerBuilder<GenericRecord, GenericRecord>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group-avro",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    )
    .SetKeyDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync())
    .SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync())
    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
    .Build();

    var jsonSerializerConfig = new JsonSerializerConfig
    {
      AutoRegisterSchemas = true,
    };
    var producerJson = new ProducerBuilder<TestKey, TestValue>(
      new ProducerConfig
      {
        BootstrapServers = "broker:29092",
      }
    )
    .SetKeySerializer(new JsonSerializer<TestKey>(schemaRegistry, jsonSerializerConfig))
    .SetValueSerializer(new JsonSerializer<TestValue>(schemaRegistry, jsonSerializerConfig))
    .Build();
    var avroSerializerConfig = new AvroSerializerConfig
    {
      AutoRegisterSchemas = true,
    };
    var producerAvro = new ProducerBuilder<GenericRecord, GenericRecord>(
      new ProducerConfig
      {
        BootstrapServers = "broker:29092",
      }
    )
    .SetKeySerializer(new AvroSerializer<GenericRecord>(schemaRegistry, avroSerializerConfig))
    .SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry, avroSerializerConfig))
    .Build();

    var driverJson = new KafkaDriver<TestKey, TestValue>(this._adminClient, consumer, producerJson);
    var driverAvro = new KafkaDriver<GenericRecord, GenericRecord>(this._adminClient, consumer, producerAvro);

    this._dbFixturesJson = new DbFixtures.DbFixtures([driverJson]);
    this._dbFixturesAvro = new DbFixtures.DbFixtures([driverAvro]);

    this._sutJson = new Kafka<TestKey, TestValue>(
      KafkaUtilsJson.PrepareInputs(
        schemaRegistryConfig,
        new ProducerConfig
        {
          BootstrapServers = "broker:29092",
        },
        new ConsumerConfig
        {
          BootstrapServers = "broker:29092",
          GroupId = "example-consumer-group-json",
          AutoOffsetReset = AutoOffsetReset.Earliest,
          EnableAutoCommit = false,
        }
      )
    );
    this._sutAvro = new Kafka<GenericRecord, GenericRecord>(
      KafkaUtilsAvro.PrepareInputs(
        schemaRegistryConfig,
        new ProducerConfig
        {
          BootstrapServers = "broker:29092",
        },
        new ConsumerConfig
        {
          BootstrapServers = "broker:29092",
          GroupId = "example-consumer-group-avro",
          AutoOffsetReset = AutoOffsetReset.Earliest,
          EnableAutoCommit = false,
        },
        null,
        SchemaFormat.Avro
      )
    );
  }

  public void Dispose()
  {
    this._dbFixturesJson.CloseDrivers();
    this._dbFixturesAvro.CloseDrivers();
    this._realConsumerJson.Close();
    this._realConsumerJson.Dispose();
    this._realConsumerAvro.Close();
    this._realConsumerAvro.Dispose();
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
        new TopicSpecification { Name = JSON_TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 },
        new TopicSpecification { Name = AVRO_TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 },
      });
    }
    catch (CreateTopicsException ex) when (
      ex.Results.All(r => r.Error.IsError == false || r.Error.Code == ErrorCode.TopicAlreadyExists)
    )
    { }
  }

  [Fact]
  public async Task Publish_IfWorkingWithJsonSchemas_ItShouldInsertEventInTopic()
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

    await this._dbFixturesJson.InsertFixtures<Message<TestKey, TestValue>>(
      [JSON_TOPIC_NAME],
      new Dictionary<string, Message<TestKey, TestValue>[]>
      {
        { JSON_TOPIC_NAME, [ expectedMessages[0] ] },
      }
    );

    var cts = new CancellationTokenSource(5000);

    DeliveryResult<TestKey, TestValue>? delRes = null;
    Exception? exception = null;

    this._sutJson.Publish(
      JSON_TOPIC_NAME,
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

    this._realConsumerJson.Subscribe(JSON_TOPIC_NAME);
    List<Message<TestKey, TestValue>> records = new List<Message<TestKey, TestValue>> { };
    while (records.Count < expectedMessages.Length)
    {
      var cr = _realConsumerJson.Consume(TimeSpan.FromSeconds(1));
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
  public async Task Publish_IfWorkingWithAvroSchemas_ItShouldInsertEventInTopic()
  {
    var keySchema = (RecordSchema)Avro.Schema.Parse(@"{
      ""type"": ""record"", ""name"": ""TestKey"", ""namespace"": ""Toolkit.Tests.Integration"",
      ""fields"": [ { ""name"": ""id"", ""type"": ""string"" } ]
    }");
    var valueSchema = (RecordSchema)Avro.Schema.Parse(@"{
      ""type"": ""record"", ""name"": ""TestValue"", ""namespace"": ""Toolkit.Tests.Integration"",
      ""fields"": [ { ""name"": ""name"", ""type"": ""string"" } ]
    }");

    var key1 = new GenericRecord(keySchema);
    key1.Add("id", "seed id 1");
    var value1 = new GenericRecord(valueSchema);
    value1.Add("name", "seed name 1");
    var key2 = new GenericRecord(keySchema);
    key2.Add("id", "test id");
    var value2 = new GenericRecord(valueSchema);
    value2.Add("name", "test name");

    Message<GenericRecord, GenericRecord>[] expectedMessages = [
      new Message<GenericRecord, GenericRecord>
      {
        Key = key1,
        Value = value1,
      },
      new Message<GenericRecord, GenericRecord>
      {
        Key = key2,
        Value = value2,
      },
    ];

    await this._dbFixturesAvro.InsertFixtures<Message<GenericRecord, GenericRecord>>(
      [AVRO_TOPIC_NAME],
      new Dictionary<string, Message<GenericRecord, GenericRecord>[]>
      {
        { AVRO_TOPIC_NAME, [ expectedMessages[0] ] },
      }
    );

    var cts = new CancellationTokenSource(5000);

    DeliveryResult<GenericRecord, GenericRecord>? delRes = null;
    Exception? exception = null;

    this._sutAvro.Publish(
      AVRO_TOPIC_NAME,
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

    this._realConsumerAvro.Subscribe(AVRO_TOPIC_NAME);
    List<Message<GenericRecord, GenericRecord>> records = new List<Message<GenericRecord, GenericRecord>> { };
    while (records.Count < expectedMessages.Length)
    {
      var cr = _realConsumerAvro.Consume(TimeSpan.FromSeconds(1));
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
  public async Task Subscribe_IfWorkingWithJsonSchemas_WithCTS_ItShouldConsumeEventsFromTopic()
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

    await this._dbFixturesJson.InsertFixtures<Message<TestKey, TestValue>>(
      [JSON_TOPIC_NAME],
      new Dictionary<string, Message<TestKey, TestValue>[]>
      {
        { JSON_TOPIC_NAME, expectedMessages },
      }
    );

    List<Message<TestKey, TestValue>> records = new List<Message<TestKey, TestValue>> { };
    List<Exception> exceptions = new List<Exception> { };
    var cts = new CancellationTokenSource(5000);
    this._sutJson.Subscribe(
      [JSON_TOPIC_NAME],
      (res, ex) =>
      {
        if (ex != null)
        {
          exceptions.Add(ex);
        }

        if (res != null)
        {
          var msg = res.Message;
          msg.Timestamp = Timestamp.Default;
          msg.Headers = null;
          records.Add(msg);

          this._sutJson.Commit(res);
        }

        if (records.Count == expectedMessages.Length)
        {
          cts.Cancel();
        }
      },
      cts,
      0
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

  [Fact]
  public async Task Subscribe_IfWorkingWithAvroSchemas_WithCTS_ItShouldConsumeEventsFromTopic()
  {
    var keySchema = (RecordSchema)Avro.Schema.Parse(@"{
      ""type"": ""record"", ""name"": ""TestKey"", ""namespace"": ""Toolkit.Tests.Integration"",
      ""fields"": [ { ""name"": ""id"", ""type"": ""string"" } ]
    }");
    var valueSchema = (RecordSchema)Avro.Schema.Parse(@"{
      ""type"": ""record"", ""name"": ""TestValue"", ""namespace"": ""Toolkit.Tests.Integration"",
      ""fields"": [ { ""name"": ""name"", ""type"": ""string"" } ]
    }");

    var key1 = new GenericRecord(keySchema);
    key1.Add("id", "seed id 1");
    var value1 = new GenericRecord(valueSchema);
    value1.Add("name", "seed name 1");
    var key2 = new GenericRecord(keySchema);
    key2.Add("id", "test id");
    var value2 = new GenericRecord(valueSchema);
    value2.Add("name", "test name");

    Message<GenericRecord, GenericRecord>[] expectedMessages = [
      new Message<GenericRecord, GenericRecord>
      {
        Key = key1,
        Value = value1,
      },
      new Message<GenericRecord, GenericRecord>
      {
        Key = key2,
        Value = value2,
      },
    ];

    await this._dbFixturesAvro.InsertFixtures<Message<GenericRecord, GenericRecord>>(
      [AVRO_TOPIC_NAME],
      new Dictionary<string, Message<GenericRecord, GenericRecord>[]>
      {
        { AVRO_TOPIC_NAME, expectedMessages },
      }
    );

    List<Message<GenericRecord, GenericRecord>> records = new List<Message<GenericRecord, GenericRecord>> { };
    List<Exception> exceptions = new List<Exception> { };
    var cts = new CancellationTokenSource(5000);
    this._sutAvro.Subscribe(
      [AVRO_TOPIC_NAME],
      (res, ex) =>
      {
        if (ex != null)
        {
          exceptions.Add(ex);
        }

        if (res != null)
        {
          var msg = res.Message;
          msg.Timestamp = Timestamp.Default;
          msg.Headers = null;
          records.Add(msg);

          this._sutAvro.Commit(res);
        }

        if (records.Count == expectedMessages.Length)
        {
          cts.Cancel();
        }
      },
      cts,
      0
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