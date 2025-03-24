using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the use of ProducerBuilder and ConsumerBuilder, from the Confluent SDK, is on non-overwritable methods.")]
public static class Kafka<TKey, TValue>
where TValue : class
{
  public static KafkaInputs<TKey, TValue> PrepareInputs(
    ISchemaRegistryClient schemaRegistry, string schemaSubject,
    int schemaVersion, JsonSerializer<TValue> jsonSerializer,
    ProducerBuilder<TKey, TValue>? producerBuilder = null,
    ConsumerBuilder<TKey, TValue>? consumerBuilder = null
  )
  {
    IProducer<TKey, TValue>? producer = null;
    if (producerBuilder != null)
    {
      producer = producerBuilder.SetValueSerializer(jsonSerializer).Build();
    }

    IConsumer<TKey, TValue>? consumer = null;
    if (consumerBuilder != null)
    {
      consumer = consumerBuilder
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