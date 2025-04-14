using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Toolkit;
using Toolkit.Types;
using KafkaUtils = Toolkit.Utils.Kafka<string, dynamic>;

namespace Tester.Services;

class Kafka
{
  private readonly IKafka<string, dynamic> _kafka;
  private readonly IFeatureFlags _ff;
  private CancellationTokenSource? _cts;

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

    KafkaInputs<string, dynamic> kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, "myTestTopic-value", 1, producerConfig, consumerConfig
    );
    this._kafka = new Kafka<string, dynamic>(kafkaInputs);

    app.MapPost("/kafka", () =>
    {
      this._kafka.Publish(
        "myTestTopic",
        new Message<string, dynamic> { Key = DateTime.UtcNow.ToString(), Value = document },
        (res) => { Console.WriteLine($"Event inserted in partition: {res.Partition} and offset: {res.Offset}."); }
      );
    });

    this._ff = featureFlags;
    string ffKey = "ctt-net-toolkit-tester-consume-kafka-events";

    featureFlags.SubscribeToValueChanges(
      ffKey,
      (ev) =>
      {
        if (ev.NewValue.AsBool)
        {
          SubToTopic();
        }
        else
        {
          UnSubToTopic();
        }
      }
    );

    if (featureFlags.GetBoolFlagValue(ffKey))
    {
      SubToTopic();
    }
  }

  private void SubToTopic()
  {
    Console.WriteLine("Subscribing to Kafka topic.");

    this._cts = new CancellationTokenSource();

    this._kafka.Subscribe(
      ["myTestTopic"],
      (res) =>
      {
        Console.WriteLine($"Processing event from partition: {res.Partition} | offset: {res.Offset}");
        Console.WriteLine(res.Message.Value);
        this._kafka.Commit(res);
      },
      this._cts
    );
  }

  private void UnSubToTopic()
  {
    if (this._cts == null) { return; }

    Console.WriteLine("Un-Subscribing to Kafka topic.");
    this._cts.Cancel();
  }
}