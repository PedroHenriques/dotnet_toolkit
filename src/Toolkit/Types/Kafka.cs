using Confluent.Kafka;
using Confluent.SchemaRegistry;

namespace Toolkit.Types;

public struct KafkaInputs<TKey, TValue>
where TValue : class
{
  public required ISchemaRegistryClient SchemaRegistry { get; set; }
  public required string SchemaSubject { get; set; }
  public required int SchemaVersion { get; set; }
  public IProducer<TKey, TValue>? Producer { get; set; }
  public IConsumer<TKey, TValue>? Consumer { get; set; }
  public IFeatureFlags? FeatureFlags { get; set; }
}

public interface IKafka<TKey, TValue>
{
  public void Publish(
    string topicName, Message<TKey, TValue> message,
    Action<DeliveryResult<TKey, TValue>> handler
  );

  public void Subscribe(
    IEnumerable<string> topics,
    Action<ConsumeResult<TKey, TValue>> handler,
    CancellationTokenSource? consumerCTS = null
  );

  public void Subscribe(
    IEnumerable<string> topics,
    Action<ConsumeResult<TKey, TValue>> handler,
    string featureFlagKey
  );

  public void Commit(ConsumeResult<TKey, TValue> consumeResult);
}