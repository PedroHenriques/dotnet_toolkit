using Confluent.Kafka;
using Confluent.SchemaRegistry;

namespace Toolkit.Types;

public enum SchemaFormat
{
  Json,
  Avro,
}

public struct KafkaInputs<TKey, TValue>
where TValue : class
{
  public required ISchemaRegistryClient SchemaRegistry { get; set; }
  public IProducer<TKey, TValue>? Producer { get; set; }
  public IConsumer<TKey, TValue>? Consumer { get; set; }
  public IFeatureFlags? FeatureFlags { get; set; }
  public ILogger? Logger { get; set; }
  public string? TraceIdPath { get; set; }
  public string? ActivitySourceName { get; set; }
  public string? ActivityName { get; set; }
}

public interface IKafka<TKey, TValue>
{
  public void Publish(
    string topicName, Message<TKey, TValue> message,
    Action<DeliveryResult<TKey, TValue>?, Exception?> handler
  );

  public void Subscribe(
    IEnumerable<string> topics,
    Func<ConsumeResult<TKey, TValue>?, Exception?, Task> handler,
    CancellationTokenSource? consumerCTS = null, double pollingDelaySec = 0
  );

  public void Subscribe(
    IEnumerable<string> topics,
    Func<ConsumeResult<TKey, TValue>?, Exception?, Task> handler,
    string featureFlagKey, double pollingDelaySec = 0
  );

  public void Commit(ConsumeResult<TKey, TValue> consumeResult);
}