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

  public void Subscribe(
    IEnumerable<string> topics,
    Action<ConsumeResult<TKey, TValue>> handler,
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
            handler(consumeResult);
          }
          catch (ConsumeException e)
          {
            Console.WriteLine($"Consume error: {e.Error.Reason}");
          }
        }
      }
      catch (OperationCanceledException)
      {
        Console.WriteLine($"OperationCanceledException thrown");
        this._inputs.Consumer.Close();
      }
      catch (Exception e)
      {
        Console.WriteLine($"Exception thrown: {e.Message}");
      }
    });
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