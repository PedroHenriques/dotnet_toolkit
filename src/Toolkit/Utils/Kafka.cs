using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the Confluent SDK is done.")]
public static class Kafka<TKey, TValue>
where TValue : class
{
  public static KafkaInputs<TKey, TValue> PrepareInputs(
    SchemaRegistryConfig schemaRegistryConfig, string schemaSubject,
    int schemaVersion, ProducerConfig? producerConfig = null,
    ConsumerConfig? consumerConfig = null
  )
  {
    ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(
      schemaRegistryConfig
    );

    IProducer<TKey, TValue>? producer = null;
    if (producerConfig != null)
    {
      producer = new ProducerBuilder<TKey, TValue>(producerConfig)
        .SetValueSerializer(new JsonSerializer<TValue>(schemaRegistry))
        .Build();
    }

    IConsumer<TKey, TValue>? consumer = null;
    if (consumerConfig != null)
    {
      consumer = new ConsumerBuilder<TKey, TValue>(consumerConfig)
        .SetValueDeserializer(new JsonDeserializer<TValue>().AsSyncOverAsync())
        .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
        .Build();
    }

    return new KafkaInputs<TKey, TValue>
    {
      SchemaRegistry = schemaRegistry,
      SchemaSubject = schemaSubject,
      SchemaVersion = schemaVersion,
      Producer = producer,
      Consumer = consumer,
    };
  }
}