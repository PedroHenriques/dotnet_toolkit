using System.Diagnostics;
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

            // Not unit testable since the unit test suite will not be able to
            // regiter an Activity listener that can capture the activity created here.
            if (String.IsNullOrEmpty(this._inputs.TraceIdPath) == false)
            {
              string? msgTraceId = (string?)Utilities.GetByPath(
                consumeResult.Message.Value, this._inputs.TraceIdPath
              );

              var activity = Logger.SetTraceIds(
                msgTraceId ?? ActivityTraceId.CreateRandom().ToString(),
                this._inputs.ActivitySourceName ?? "Toolkit default activity source name",
                this._inputs.ActivityName ?? "Toolkit default activity name"
              );

              if (
                this._inputs.Logger != null && activity != null &&
                activity.TraceId.ToString() != msgTraceId
              )
              {
                this._inputs.Logger.Log(
                  Microsoft.Extensions.Logging.LogLevel.Warning,
                  null,
                  $"The message received from the topic '{consumeResult.Topic}', partition '{consumeResult.Partition.Value}' and offset '{consumeResult.Offset.Value}' had an invalid value for a trace ID in the node '{this._inputs.TraceIdPath}': '{msgTraceId}'"
                );
              }
            }
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