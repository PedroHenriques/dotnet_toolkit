using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Toolkit;
using Toolkit.Types;
using KafkaUtilsJson = Toolkit.Utils.Kafka<MyKey, MyValue>;
using KafkaUtilsAvro = Toolkit.Utils.Kafka<Avro.Generic.GenericRecord, Avro.Generic.GenericRecord>;

namespace Tester.Services;

class Kafka
{
  public Kafka(WebApplication app, MyValue document, IFeatureFlags featureFlags, Microsoft.Extensions.Logging.ILogger logger)
  {
    logger.BeginScope(
      new Dictionary<string, object?>
      {
        ["scope.prop"] = "test kafka prop",
        ["hello from scope"] = "world from kafka",
      }
    );
    logger.Log(LogLevel.Critical, null, "Test message with scope and structured attributes '{someKafkaProp}' from the Kafka service", "some kafka prop");

    string? schemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL");
    if (schemaRegistryUrl == null)
    {
      throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");
    }
    var schemaRegistryConfig = new SchemaRegistryConfig { Url = schemaRegistryUrl };

    string? kafkaConStr = Environment.GetEnvironmentVariable("KAFKA_CON_STR");
    if (kafkaConStr == null)
    {
      throw new Exception("Could not get the 'KAFKA_CON_STR' environment variable");
    }
    var producerConfig = new ProducerConfig
    {
      BootstrapServers = kafkaConStr,
    };
    var consumerConfigJson = new ConsumerConfig
    {
      BootstrapServers = kafkaConStr,
      GroupId = "example-consumer-group-json",
      AutoOffsetReset = AutoOffsetReset.Latest,
      EnableAutoCommit = false,
    };

    KafkaInputs<MyKey, MyValue> kafkaInputsJson = KafkaUtilsJson.PrepareInputs(
      schemaRegistryConfig, producerConfig, consumerConfigJson, featureFlags
    );
    IKafka<MyKey, MyValue> kafkaJson = new Kafka<MyKey, MyValue>(kafkaInputsJson);

    var consumerConfigAvro = new ConsumerConfig
    {
      BootstrapServers = kafkaConStr,
      GroupId = "example-consumer-group-avro",
      AutoOffsetReset = AutoOffsetReset.Latest,
      EnableAutoCommit = false,
    };
    KafkaInputs<GenericRecord, GenericRecord> kafkaInputsAvro = KafkaUtilsAvro.PrepareInputs(
      schemaRegistryConfig, producerConfig, consumerConfigAvro, featureFlags, SchemaFormat.Avro
    );
    IKafka<GenericRecord, GenericRecord> kafkaAvro = new Kafka<GenericRecord, GenericRecord>(kafkaInputsAvro);

    app.MapPost("/kafka/json", () =>
    {
      var key = new MyKey { Id = DateTime.UtcNow.ToString() };

      kafkaJson.Publish(
        "myTestTopicJson",
        new Message<MyKey, MyValue> { Key = key, Value = document },
        (res, ex) =>
        {
          if (ex != null)
          {
            Console.WriteLine($"Event not inserted in topic 'myTestTopicJson' with error: {ex}");
            return;
          }
          if (res == null)
          {
            Console.WriteLine("kafka.Publish() callback invoked with NULL res, for topic 'myTestTopicJson'.");
            return;
          }
          Console.WriteLine($"Event inserted in topic 'myTestTopicJson', partition: {res.Partition} and offset: {res.Offset}.");
        }
      );
    });

    kafkaJson.Subscribe(
      ["myTestTopicJson"],
      (res, ex) =>
      {
        if (ex != null)
        {
          Console.WriteLine($"Event not consumed from topic 'myTestTopicJson' with error: {ex}");
          return;
        }
        if (res == null)
        {
          Console.WriteLine("kafka.Subscribe() callback invoked with NULL res, for topic 'myTestTopicJson'.");
          return;
        }
        Console.WriteLine($"Processing event from topic 'myTestTopicJson', partition: {res.Partition} and offset: {res.Offset}");
        Console.WriteLine(res.Message.Key);
        Console.WriteLine(res.Message.Value);
        kafkaJson.Commit(res);
      },
      "ctt-net-toolkit-tester-consume-kafka-events"
    );

    app.MapPost("/kafka/avro", () =>
    {
      var keySchema = (RecordSchema)Avro.Schema.Parse(@"{
        ""type"": ""record"", ""name"": ""MyKey"", ""namespace"": ""Tester.Services"",
        ""fields"": [ { ""name"": ""id"", ""type"": ""string"" } ]
      }");
      var valueSchema = (RecordSchema)Avro.Schema.Parse(@"{
        ""type"": ""record"", ""name"": ""MyValue"", ""namespace"": ""Tester.Services"",
        ""fields"": [ { ""name"": ""name"", ""type"": ""string"" } ]
      }");

      var key = new GenericRecord(keySchema);
      key.Add("id", DateTime.UtcNow.ToString());
      var value = new GenericRecord(valueSchema);
      value.Add("name", $"hello world: {DateTime.Now}");

      kafkaAvro.Publish(
        "myTestTopicAvro",
        new Message<GenericRecord, GenericRecord> { Key = key, Value = value },
        (res, ex) =>
        {
          if (ex != null)
          {
            Console.WriteLine($"Event not inserted in topic 'myTestTopicAvro' with error: {ex}");
            return;
          }
          if (res == null)
          {
            Console.WriteLine("kafka.Publish() callback invoked with NULL res, for topic 'myTestTopicAvro'.");
            return;
          }
          Console.WriteLine($"Event inserted in topic 'myTestTopicAvro', partition: {res.Partition} and offset: {res.Offset}.");
        }
      );
    });

    kafkaAvro.Subscribe(
      ["myTestTopicAvro"],
      (res, ex) =>
      {
        if (ex != null)
        {
          Console.WriteLine($"Event not consumed from topic 'myTestTopicAvro' with error: {ex}");
          return;
        }
        if (res == null)
        {
          Console.WriteLine("kafka.Subscribe() callback invoked with NULL res, for topic 'myTestTopicAvro'.");
          return;
        }
        Console.WriteLine($"Processing event from topic 'myTestTopicAvro', partition: {res.Partition} and offset: {res.Offset}");
        Console.WriteLine(res.Message.Key);
        Console.WriteLine(res.Message.Value);
        kafkaAvro.Commit(res);
      },
      "ctt-net-toolkit-tester-consume-kafka-events"
    );
  }
}