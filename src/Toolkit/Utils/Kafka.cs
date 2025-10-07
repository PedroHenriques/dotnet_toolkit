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
    ConsumerConfig? consumerConfig = null, IFeatureFlags? featureFlags = null,
    SchemaFormat schemaFormat = SchemaFormat.Json
  )
  {
    ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(
      schemaRegistryConfig
    );

    IProducer<TKey, TValue>? producer = null;
    if (producerConfig != null)
    {
      producerConfig.AllowAutoCreateTopics = false;

      var producerBuilder = new ProducerBuilder<TKey, TValue>(producerConfig);

      switch (schemaFormat)
      {
        case SchemaFormat.Avro:
          var avroSerializerConfig = new AvroSerializerConfig
          {
            AutoRegisterSchemas = false,
          };
          producerBuilder
            .SetKeySerializer(new AvroSerializer<TKey>(schemaRegistry, avroSerializerConfig))
            .SetValueSerializer(new AvroSerializer<TValue>(schemaRegistry, avroSerializerConfig));
          break;
        case SchemaFormat.Json:
          var jsonSerializerConfig = new JsonSerializerConfig
          {
            AutoRegisterSchemas = false,
          };
          producerBuilder
            .SetKeySerializer(new JsonSerializer<TKey>(schemaRegistry, jsonSerializerConfig))
            .SetValueSerializer(new JsonSerializer<TValue>(schemaRegistry, jsonSerializerConfig));
          break;
        default:
          throw new Exception($"The schema format received ({schemaFormat}) is not supported.");
      }

      producer = producerBuilder.Build();
    }

    IConsumer<TKey, TValue>? consumer = null;
    if (consumerConfig != null)
    {
      consumerConfig.AllowAutoCreateTopics = false;
      consumerConfig.EnableAutoCommit = false;

      var consumerBuilder = new ConsumerBuilder<TKey, TValue>(consumerConfig);

      switch (schemaFormat)
      {
        case SchemaFormat.Avro:
          consumerBuilder
            .SetKeyDeserializer(new AvroDeserializer<TKey>(schemaRegistry).AsSyncOverAsync())
            .SetValueDeserializer(new AvroDeserializer<TValue>(schemaRegistry).AsSyncOverAsync());
          break;
        case SchemaFormat.Json:
          consumerBuilder
            .SetKeyDeserializer(new JsonDeserializer<TKey>(schemaRegistry).AsSyncOverAsync())
            .SetValueDeserializer(new JsonDeserializer<TValue>(schemaRegistry).AsSyncOverAsync());
          break;
        default:
          throw new Exception($"The schema format received ({schemaFormat}) is not supported.");
      }

      consumer = consumerBuilder
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