using System.Dynamic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Toolkit;
using Toolkit.Types;
using KafkaUtils = Toolkit.Utils.Kafka<MyKey, MyValue>;

namespace Tester.Services;

class Kafka
{
  public Kafka(WebApplication app, MyValue document, IFeatureFlags featureFlags)
  {
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
    var consumerConfig = new ConsumerConfig
    {
      BootstrapServers = kafkaConStr,
      GroupId = "example-consumer-group",
      AutoOffsetReset = AutoOffsetReset.Latest,
      EnableAutoCommit = false,
    };

    KafkaInputs<MyKey, MyValue> kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, "myTestTopic-value", 1, producerConfig, consumerConfig, featureFlags
    );
    IKafka<MyKey, MyValue> kafka = new Kafka<MyKey, MyValue>(kafkaInputs);

    app.MapPost("/kafka", () =>
    {
      var key = new MyKey { Id = DateTime.UtcNow.ToString() };

      kafka.Publish(
        "myTestTopic",
        new Message<MyKey, MyValue> { Key = key, Value = document },
        (res, ex) =>
        {
          if (ex != null)
          {
            Console.WriteLine($"Event not inserted with error: {ex}");
            return;
          }
          if (res == null)
          {
            Console.WriteLine("kafka.Publish() callback invoked with NULL res.");
            return;
          }
          Console.WriteLine($"Event inserted in partition: {res.Partition} and offset: {res.Offset}.");
        }
      );
    });

    kafka.Subscribe(
      ["myTestTopic"],
      (res, ex) =>
      {
        if (ex != null)
        {
          Console.WriteLine($"Event not inserted with error: {ex}");
          return;
        }
        if (res == null)
        {
          Console.WriteLine("kafka.Subscribe() callback invoked with NULL res.");
          return;
        }
        Console.WriteLine($"Processing event from partition: {res.Partition} | offset: {res.Offset}");
        Console.WriteLine(res.Message.Key);
        Console.WriteLine(res.Message.Value);
        kafka.Commit(res);
      },
      "ctt-net-toolkit-tester-consume-kafka-events"
    );
  }
}