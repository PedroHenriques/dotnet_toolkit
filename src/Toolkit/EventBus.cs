using Confluent.Kafka;
using Toolkit.Types;

namespace Toolkit;

public class EventBus<TKey, TValue> : IEventBus<TKey, TValue>
where TValue : class
{
  private readonly EventBusInputs<TKey, TValue> _inputs;

  public EventBus(EventBusInputs<TKey, TValue> inputs)
  {
    this._inputs = inputs;
  }

  public void Publish(
    string topicName, Message<TKey, TValue> message,
    Action<DeliveryResult<TKey, TValue>> handler
  )
  {
    if (this._inputs.Producer == null)
    {
      throw new Exception("An instance of IProducer was not provided in the inputs.");
    }

    this._inputs.Producer.ProduceAsync(topicName, message)
      .ContinueWith((result) =>
      {
        handler(result.Result);
      });

    this._inputs.Producer.Flush();
  }
}