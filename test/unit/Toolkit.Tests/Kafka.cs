using Confluent.Kafka;
using Confluent.SchemaRegistry;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class KafkaTests : IDisposable
{
  private readonly Mock<ISchemaRegistryClient> _schemaRegistryMock;
  private readonly Mock<IProducer<string, string>> _producerMock;
  private readonly Mock<IConsumer<string, string>> _consumerMock;
  private readonly Mock<Action<DeliveryResult<string, string>>> _handlerProducerMock;
  private readonly Mock<Action<ConsumeResult<string, string>?, Exception?>> _handlerConsumerMock;
  private readonly Mock<IFeatureFlags> _ffMock;
  private KafkaInputs<string, string> _kafkaInputs;

  public KafkaTests()
  {
    this._schemaRegistryMock = new Mock<ISchemaRegistryClient>(MockBehavior.Strict);
    this._producerMock = new Mock<IProducer<string, string>>(MockBehavior.Strict);
    this._consumerMock = new Mock<IConsumer<string, string>>(MockBehavior.Strict);
    this._handlerProducerMock = new Mock<Action<DeliveryResult<string, string>>>(MockBehavior.Strict);
    this._handlerConsumerMock = new Mock<Action<ConsumeResult<string, string>?, Exception?>>(MockBehavior.Strict);
    this._ffMock = new Mock<IFeatureFlags>(MockBehavior.Strict);

    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new DeliveryResult<string, string> { }));
    this._producerMock.Setup(s => s.Flush(It.IsAny<CancellationToken>()));

    this._consumerMock.Setup(s => s.Subscribe(It.IsAny<IEnumerable<string>>()));
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(new ConsumeResult<string, string>())
      .Throws(new OperationCanceledException());
    this._consumerMock.Setup(s => s.Commit(It.IsAny<ConsumeResult<string, string>>()));

    this._handlerProducerMock.Setup(s => s(It.IsAny<DeliveryResult<string, string>>()));
    this._handlerConsumerMock.Setup(s => s(It.IsAny<ConsumeResult<string, string>>(), It.IsAny<Exception?>()));

    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true);
    this._ffMock.Setup(s => s.SubscribeToValueChanges(It.IsAny<string>(), It.IsAny<Action<FlagValueChangeEvent>?>()));

    this._kafkaInputs = new KafkaInputs<string, string>
    {
      SchemaRegistry = this._schemaRegistryMock.Object,
      SchemaSubject = "test schema subject",
      SchemaVersion = 1,
    };
  }

  public void Dispose()
  {
    this._schemaRegistryMock.Reset();
    this._producerMock.Reset();
    this._consumerMock.Reset();
    this._handlerProducerMock.Reset();
    this._handlerConsumerMock.Reset();
    this._ffMock.Reset();
  }

  [Fact]
  public void Publish_ItShouldCallProduceAsyncFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

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
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);

    this._producerMock.Verify(m => m.Flush((CancellationToken)default), Times.Once());
  }

  [Fact]
  public async Task Publish_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var deliveryRes = new DeliveryResult<string, string> { };
    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(deliveryRes));
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);
    await Task.Delay(500);

    this._handlerProducerMock.Verify(m => m(deliveryRes), Times.Once());
  }

  [Fact]
  public void Publish_IfAProducerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new Kafka<string, string>(this._kafkaInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };

    var e = Assert.Throws<Exception>(() => sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object));
    Assert.Equal("An instance of IProducer was not provided in the inputs.", e.Message);
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_ItShouldCallSubscribeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    consumerCTS.Cancel();

    this._consumerMock.Verify(m => m.Subscribe(topics), Times.Once());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_ItShouldCallConsumeFromTheConsumerInstanceAtLeastOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    consumerCTS.Cancel();

    this._consumerMock.Verify(m => m.Consume(consumerCTS.Token), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_ItShouldCallTheHandlerReceivedAsInputAtLeastOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    consumerCTS.Cancel();

    this._handlerConsumerMock.Verify(m => m(consumeRes, null), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAConsumeExceptionIsThrown_ItShouldContinueCallingConsumeFromTheConsumerInstanceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), new Exception()));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Consume(consumerCTS.Token), Times.AtLeast(2));
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAConsumeExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAnOperationCanceledExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new OperationCanceledException("exception msg from test", innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAnExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    var innerEx = new Exception("exception msg from test");
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(innerEx);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public void Subscribe_WithCancelToken_IfAConsumerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    var e = Assert.Throws<Exception>(() => sut.Subscribe(topics, this._handlerConsumerMock.Object));
    Assert.Equal("An instance of IConsumer was not provided in the inputs.", e.Message);
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAConsumerCancellationTokenSourceWasNotProvidedInTheInputs_ItShouldCallConsumeFromTheConsumerInstanceAtLeastOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallGetBoolFlagValueFromTheFeatureFlagInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._ffMock.Verify(m => m.GetBoolFlagValue("some ff key"), Times.Once());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallSubscribeToValueChangesFromTheFeatureFlagInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._ffMock.Verify(m => m.SubscribeToValueChanges("some ff key", It.IsAny<Action<FlagValueChangeEvent>?>()), Times.Once());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallSubscribeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Subscribe(topics), Times.Once());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallConsumeFromTheConsumerInstanceAtLeastOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallTheHandlerReceivedAsInputAtLeastOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(consumeRes, null), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfTheHandlerProvidedToSubscribeToValueChangesIsInvoked_IfTheChangeEventHasAFlagValueOfFalse_ItShouldCancelTheCancellationTokenSentToConsumeFromTheConsumerInstance()
  {
    var oldValue = LdValue.Of(true);
    var newValue = LdValue.Of(false);
    var testEvent = new FlagValueChangeEvent("some ff key", oldValue, newValue);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    (this._ffMock.Invocations[1].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);
    await Task.Delay(500);

    dynamic token = this._consumerMock.Invocations[1].Arguments[0];
    Assert.True(token.IsCancellationRequested);
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfTheHandlerProvidedToSubscribeToValueChangesIsInvoked_IfTheChangeEventHasAFlagValueOfTrue_ItShouldCallSubscribeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    var oldValue = LdValue.Of(false);
    var newValue = LdValue.Of(true);
    var testEvent = new FlagValueChangeEvent("some ff key", oldValue, newValue);
    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    (this._ffMock.Invocations[1].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Subscribe(topics), Times.Once());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfTheCallToGetBoolFlagValueReturnsFalse_ItShouldNotCallSubscribeFromTheConsumerInstance()
  {
    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Subscribe(It.IsAny<IEnumerable<string>>()), Times.Never());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfTheCallToGetBoolFlagValueReturnsFalse_ItShouldCallSubscribeToValueChangesFromTheFeatureFlagInstanceOnceWithTheExpectedArguments()
  {
    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._ffMock.Verify(m => m.SubscribeToValueChanges("some ff key", It.IsAny<Action<FlagValueChangeEvent>?>()), Times.Once());
  }

  [Fact]
  public void Subscribe_WithFFKey_IfAFeatureFlagInstanceWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];

    var e = Assert.Throws<Exception>(() => sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key"));
    Assert.Equal("An instance of IFeatureFlags was not provided in the inputs.", e.Message);
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfAConsumeExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfAnOperationCanceledExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new OperationCanceledException("exception msg from test", innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfAnExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<string, string>();
    var innerEx = new Exception("exception msg from test");
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(innerEx);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public void Commit_ItShouldCallCommitFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<string, string>(this._kafkaInputs);

    var consumeRes = new ConsumeResult<string, string>();
    sut.Commit(consumeRes);

    this._consumerMock.Verify(m => m.Commit(consumeRes), Times.Once());
  }

  [Fact]
  public void Commit_IfAConsumerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new Kafka<string, string>(this._kafkaInputs);

    var e = Assert.Throws<Exception>(() => sut.Commit(new ConsumeResult<string, string>()));
    Assert.Equal("An instance of IConsumer was not provided in the inputs.", e.Message);
  }
}