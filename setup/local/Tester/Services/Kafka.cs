using System.Dynamic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Toolkit;
using Toolkit.Types;
using KafkaUtils = Toolkit.Utils.Kafka<dynamic, dynamic>;

namespace Tester.Services;

class Kafka
{
  public Kafka(WebApplication app, dynamic document, IFeatureFlags featureFlags)
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

    KafkaInputs<dynamic, dynamic> kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, "myTestTopic-value", 1, producerConfig, consumerConfig, featureFlags
    );
    IKafka<dynamic, dynamic> kafka = new Kafka<dynamic, dynamic>(kafkaInputs);

    app.MapPost("/kafka", () =>
    {
      dynamic key = new ExpandoObject();
      key.id = DateTime.UtcNow.ToString();

      kafka.Publish(
        "myTestTopic",
        new Message<dynamic, dynamic> { Key = key, Value = document },
        (res) => { Console.WriteLine($"Event inserted in partition: {res.Partition} and offset: {res.Offset}."); }
      );
    });

    kafka.Subscribe(
      ["myTestTopic"],
      (res) =>
      {
        Console.WriteLine($"Processing event from partition: {res.Partition} | offset: {res.Offset}");
        Console.WriteLine(res.Message.Value);
        kafka.Commit(res);
      },
      "ctt-net-toolkit-tester-consume-kafka-events"
    );
  }
}