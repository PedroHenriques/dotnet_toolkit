using Confluent.Kafka;
using Toolkit.Types;

namespace Toolkit;

public class Kafka<TKey, TValue> : IKafka<TKey, TValue>
where TValue : class
{
  private readonly KafkaInputs<TKey, TValue> _inputs;

  public Kafka(KafkaInputs<TKey, TValue> inputs)
  {
    this._inputs = inputs;
  }

  public void Publish(
    string topicName, Message<TKey, TValue> message,
    Action<DeliveryResult<TKey, TValue>?, Exception?> handler
  )
  {
    if (this._inputs.Producer == null)
    {
      throw new Exception("An instance of IProducer was not provided in the inputs.");
    }

    this._inputs.Producer.ProduceAsync(topicName, message)
      .ContinueWith((task) =>
      {
        if (task.IsCompletedSuccessfully)
        {
          handler(task.Result, null);
        }
        else
        {
          handler(null, task.Exception?.InnerException);
        }
      });

    this._inputs.Producer.Flush();
  }

  public void Subscribe(
    IEnumerable<string> topics,
    Action<ConsumeResult<TKey, TValue>?, Exception?> handler,
    CancellationTokenSource? consumerCTS = null
  )
  {
    if (this._inputs.Consumer == null)
    {
      throw new Exception("An instance of IConsumer was not provided in the inputs.");
    }
    if (consumerCTS == null)
    {
      consumerCTS = new CancellationTokenSource();
    }

    Task.Run(() =>
    {
      this._inputs.Consumer.Subscribe(topics);

      try
      {
        while (consumerCTS.IsCancellationRequested == false)
        {
          try
          {
            var consumeResult = this._inputs.Consumer.Consume(consumerCTS.Token);
            handler(consumeResult, null);
          }
          catch (ConsumeException e)
          {
            handler(null, e.InnerException);
          }
        }
      }
      catch (OperationCanceledException e)
      {
        handler(null, e.InnerException);
      }
      catch (Exception e)
      {
        handler(null, e);
      }
    });
  }

  public void Subscribe(
    IEnumerable<string> topics,
    Action<ConsumeResult<TKey, TValue>?, Exception?> handler,
    string featureFlagKey
  )
  {
    if (this._inputs.FeatureFlags == null)
    {
      throw new Exception("An instance of IFeatureFlags was not provided in the inputs.");
    }

    CancellationTokenSource? cts = null;

    var listen = () =>
    {
      cts = new CancellationTokenSource();
      Subscribe(topics, handler, cts);
    };

    if (this._inputs.FeatureFlags.GetBoolFlagValue(featureFlagKey))
    {
      listen();
    }

    this._inputs.FeatureFlags.SubscribeToValueChanges(
      featureFlagKey,
      (ev) =>
      {
        if (ev.NewValue.AsBool)
        {
          listen();
        }
        else
        {
          if (cts == null) { return; }
          cts.Cancel();
        }
      }
    );
  }

  public void Commit(ConsumeResult<TKey, TValue> consumeResult)
  {
    if (this._inputs.Consumer == null)
    {
      throw new Exception("An instance of IConsumer was not provided in the inputs.");
    }

    this._inputs.Consumer.Commit(consumeResult);
  }
}