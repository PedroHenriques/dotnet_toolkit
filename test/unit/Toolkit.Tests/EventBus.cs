using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class EventBusTests : IDisposable
{
  private readonly Mock<ISchemaRegistryClient> _schemaRegistryMock;
  private readonly Mock<IProducer<string, string>> _producerMock;
  private readonly Mock<IConsumer<string, string>> _consumerMock;
  private readonly Mock<Action<DeliveryResult<string, string>>> _handlerProducerMock;
  private readonly Mock<Action<ConsumeResult<string, string>>> _handlerConsumerMock;
  private EventBusInputs<string, string> _eventBusInputs;

  public EventBusTests()
  {
    this._schemaRegistryMock = new Mock<ISchemaRegistryClient>(MockBehavior.Strict);
    this._producerMock = new Mock<IProducer<string, string>>(MockBehavior.Strict);
    this._consumerMock = new Mock<IConsumer<string, string>>(MockBehavior.Strict);
    this._handlerProducerMock = new Mock<Action<DeliveryResult<string, string>>>(MockBehavior.Strict);
    this._handlerConsumerMock = new Mock<Action<ConsumeResult<string, string>>>(MockBehavior.Strict);

    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new DeliveryResult<string, string> { }));
    this._producerMock.Setup(s => s.Flush(It.IsAny<CancellationToken>()));

    this._consumerMock.Setup(s => s.Subscribe(It.IsAny<IEnumerable<string>>()));
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(new ConsumeResult<string, string>());

    this._handlerProducerMock.Setup(s => s(It.IsAny<DeliveryResult<string, string>>()));
    this._handlerConsumerMock.Setup(s => s(It.IsAny<ConsumeResult<string, string>>()));

    this._eventBusInputs = new EventBusInputs<string, string>
    {
      SchemaRegistry = this._schemaRegistryMock.Object,
      SchemaSubject = "test schema subject",
      SchemaVersion = 1,
      ConsumerCTS = new CancellationTokenSource(),
    };
  }

  public void Dispose()
  {
    this._schemaRegistryMock.Reset();
    this._producerMock.Reset();
    this._consumerMock.Reset();
    this._handlerProducerMock.Reset();
    this._handlerConsumerMock.Reset();
  }

  [Fact]
  public void Publish_ItShouldCallProduceAsyncFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);

    this._producerMock.Verify(m => m.ProduceAsync("test topic name", testMessage, default), Times.Once());
  }

  [Fact]
  public void Publish_ItShouldCallFlushFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);

    this._producerMock.Verify(m => m.Flush((CancellationToken)default), Times.Once());
  }

  [Fact]
  public async void Publish_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var deliveryRes = new DeliveryResult<string, string> { };
    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(deliveryRes));
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);
    await Task.Delay(5);

    this._handlerProducerMock.Verify(m => m(deliveryRes), Times.Once());
  }

  [Fact]
  public void Publish_IfAProducerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };

    var e = Assert.Throws<Exception>(() => sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object));
    Assert.Equal("An instance of IProducer was not provided in the inputs.", e.Message);
  }

  [Fact]
  public async void Subscribe_ItShouldCallSubscribeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Consumer = this._consumerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(5);

    this._eventBusInputs.ConsumerCTS.Cancel();

    this._consumerMock.Verify(m => m.Subscribe(topics), Times.Once());
  }

  [Fact]
  public async void Subscribe_ItShouldCallConsumeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Consumer = this._consumerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(5);

    this._eventBusInputs.ConsumerCTS.Cancel();

    this._consumerMock.Verify(m => m.Consume(this._eventBusInputs.ConsumerCTS.Token), Times.AtLeastOnce());
  }

  [Fact]
  public async void Subscribe_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>())).Returns(consumeRes);

    this._eventBusInputs.Consumer = this._consumerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(5);

    this._eventBusInputs.ConsumerCTS.Cancel();

    this._handlerConsumerMock.Verify(m => m(consumeRes), Times.AtLeastOnce());
  }

  [Fact]
  public async void Subscribe_IfAConsumeExceptionIsThrown_ItShouldContinueCallingConsumeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), new Exception()));

    this._eventBusInputs.Consumer = this._consumerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(5);

    this._consumerMock.Verify(m => m.Consume(this._eventBusInputs.ConsumerCTS.Token), Times.AtLeast(2));
  }

  [Fact]
  public async void Subscribe_IfAnOperationCanceledExceptionIsThrown_ItShouldCallCloseFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>())).Throws(new OperationCanceledException());

    this._eventBusInputs.Consumer = this._consumerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(5);

    this._consumerMock.Verify(m => m.Close(), Times.Once());
  }

  [Fact]
  public void Subscribe_IfAConsumerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new EventBus<string, string>(this._eventBusInputs);

    IEnumerable<string> topics = ["test topic name"];
    var e = Assert.Throws<Exception>(() => sut.Subscribe(topics, this._handlerConsumerMock.Object));
    Assert.Equal("An instance of IConsumer was not provided in the inputs.", e.Message);
  }
}