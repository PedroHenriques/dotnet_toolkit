using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the Confluent SDK is done.")]
public static class Kafka<TKey, TValue>
where TKey : class
where TValue : class
{
  public static KafkaInputs<TKey, TValue> PrepareInputs(
    SchemaRegistryConfig schemaRegistryConfig, ProducerConfig? producerConfig = null,
    ConsumerConfig? consumerConfig = null, IFeatureFlags? featureFlags = null
  )
  {
    ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(
      schemaRegistryConfig
    );

    IProducer<TKey, TValue>? producer = null;
    if (producerConfig != null)
    {
      producerConfig.AllowAutoCreateTopics = false;

      var jsonSerializerConfig = new JsonSerializerConfig
      {
        AutoRegisterSchemas = false,
      };

      producer = new ProducerBuilder<TKey, TValue>(producerConfig)
        .SetKeySerializer(new JsonSerializer<TKey>(schemaRegistry, jsonSerializerConfig))
        .SetValueSerializer(new JsonSerializer<TValue>(schemaRegistry, jsonSerializerConfig))
        .Build();
    }

    IConsumer<TKey, TValue>? consumer = null;
    if (consumerConfig != null)
    {
      consumerConfig.AllowAutoCreateTopics = false;
      consumerConfig.EnableAutoCommit = false;

      consumer = new ConsumerBuilder<TKey, TValue>(consumerConfig)
        .SetKeyDeserializer(new JsonDeserializer<TKey>(schemaRegistry).AsSyncOverAsync())
        .SetValueDeserializer(new JsonDeserializer<TValue>(schemaRegistry).AsSyncOverAsync())
        .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
        .Build();
    }

    return new KafkaInputs<TKey, TValue>
    {
      SchemaRegistry = schemaRegistry,
      Producer = producer,
      Consumer = consumer,
      FeatureFlags = featureFlags,
    };
  }
}