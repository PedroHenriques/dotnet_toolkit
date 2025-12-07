using System.Diagnostics;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Newtonsoft.Json;
using Toolkit.Types;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class KafkaTests : IDisposable
{
  private readonly Mock<ISchemaRegistryClient> _schemaRegistryMock;
  private readonly Mock<IProducer<MyKey, MyValue>> _producerMock;
  private readonly Mock<IConsumer<MyKey, MyValue>> _consumerMock;
  private readonly Mock<Action<DeliveryResult<MyKey, MyValue>?, Exception?>> _handlerProducerMock;
  private readonly Mock<Action<ConsumeResult<MyKey, MyValue>?, Exception?>> _handlerConsumerMock;
  private readonly Mock<IFeatureFlags> _ffMock;
  private readonly Mock<ILogger> _loggerMock;
  private KafkaInputs<MyKey, MyValue> _kafkaInputs;

  public KafkaTests()
  {
    this._schemaRegistryMock = new Mock<ISchemaRegistryClient>(MockBehavior.Strict);
    this._producerMock = new Mock<IProducer<MyKey, MyValue>>(MockBehavior.Strict);
    this._consumerMock = new Mock<IConsumer<MyKey, MyValue>>(MockBehavior.Strict);
    this._handlerProducerMock = new Mock<Action<DeliveryResult<MyKey, MyValue>?, Exception?>>(MockBehavior.Strict);
    this._handlerConsumerMock = new Mock<Action<ConsumeResult<MyKey, MyValue>?, Exception?>>(MockBehavior.Strict);
    this._ffMock = new Mock<IFeatureFlags>(MockBehavior.Strict);
    this._loggerMock = new Mock<ILogger>(MockBehavior.Strict);

    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<MyKey, MyValue>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new DeliveryResult<MyKey, MyValue> { }));
    this._producerMock.Setup(s => s.Flush(It.IsAny<CancellationToken>()));

    this._consumerMock.Setup(s => s.Subscribe(It.IsAny<IEnumerable<string>>()));
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(new ConsumeResult<MyKey, MyValue>())
      .Throws(new OperationCanceledException());
    this._consumerMock.Setup(s => s.Commit(It.IsAny<ConsumeResult<MyKey, MyValue>>()));

    this._handlerProducerMock.Setup(s => s(It.IsAny<DeliveryResult<MyKey, MyValue>>(), It.IsAny<Exception?>()));
    this._handlerConsumerMock.Setup(s => s(It.IsAny<ConsumeResult<MyKey, MyValue>>(), It.IsAny<Exception?>()));

    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true);
    this._ffMock.Setup(s => s.SubscribeToValueChanges(It.IsAny<string>(), It.IsAny<Action<FlagValueChangeEvent>?>()));

    this._loggerMock.Setup(s => s.Log(It.IsAny<Microsoft.Extensions.Logging.LogLevel>(), It.IsAny<Exception?>(), It.IsAny<string>()));

    this._kafkaInputs = new KafkaInputs<MyKey, MyValue>
    {
      SchemaRegistry = this._schemaRegistryMock.Object,
      Logger = this._loggerMock.Object,
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
    this._loggerMock.Reset();
  }

  [Fact]
  public void Publish_ItShouldCallProduceAsyncFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var testMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue { },
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);

    this._producerMock.Verify(m => m.ProduceAsync("test topic name", testMessage, default), Times.Once());
  }

  [Fact]
  public void Publish_ItShouldCallFlushFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var testMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue { },
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);

    this._producerMock.Verify(m => m.Flush((CancellationToken)default), Times.Once());
  }

  [Fact]
  public async Task Publish_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var deliveryRes = new DeliveryResult<MyKey, MyValue> { };
    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<MyKey, MyValue>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(deliveryRes));
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var testMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue { },
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);
    await Task.Delay(500);

    this._handlerProducerMock.Verify(m => m(deliveryRes, null), Times.Once());
  }

  [Fact]
  public async Task Publish_IfTheReturnOfCallingProduceAsyncFromTheProducerInstanceIsATaskThatDidNotCompleteSuccessfully_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var deliveryRes = new DeliveryResult<MyKey, MyValue> { };
    var testEx = new Exception("test error msg");
    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<MyKey, MyValue>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromException<DeliveryResult<MyKey, MyValue>>(testEx));
    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var testMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue { },
    };
    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);
    await Task.Delay(500);

    this._handlerProducerMock.Verify(m => m(null, testEx), Times.Once());
  }

  [Fact]
  public void Publish_IfAProducerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var testMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue { },
    };

    var e = Assert.Throws<Exception>(() => sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object));
    Assert.Equal("An instance of IProducer was not provided in the inputs.", e.Message);
  }

  [Fact]
  public void Publish_IfATraceIdPathWasProvidedInTheInputs_ItShouldAddToTheMessageValueTheCurrentActivityTraceId()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    var activity = source.StartActivity();

    this._kafkaInputs.ActivitySourceName = activitySourceName;
    this._kafkaInputs.ActivityName = "test an";
    this._kafkaInputs.TraceIdPath = "CorrelationId";

    this._kafkaInputs.Producer = this._producerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var testMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue { },
    };
    var expectedMessage = new Message<MyKey, MyValue>
    {
      Key = new MyKey { Id = "test msg key" },
      Value = new MyValue
      {
        CorrelationId = activity.TraceId.ToString(),
      },
    };

    sut.Publish("test topic name", testMessage, this._handlerProducerMock.Object);

    Assert.Equal(
      JsonConvert.SerializeObject(expectedMessage),
      JsonConvert.SerializeObject(testMessage)
    );
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_ItShouldCallSubscribeFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    consumerCTS.Cancel();

    this._consumerMock.Verify(m => m.Consume(consumerCTS.Token), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_ItShouldCallTheHandlerReceivedAsInputAtLeastOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>
    {
      Message = new Message<MyKey, MyValue>
      {
        Value = new MyValue
        {
          Id = ActivityTraceId.CreateRandom().ToString(),
        },
      },
      Partition = new Partition(123),
      Offset = new Offset(987),
      Topic = "test topic",
    };
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.TraceIdPath = "Id";
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    consumerCTS.Cancel();

    this._handlerConsumerMock.Verify(m => m(consumeRes, null), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAConsumeExceptionIsThrown_ItShouldContinueCallingConsumeFromTheConsumerInstanceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), new Exception()));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Consume(consumerCTS.Token), Times.AtLeast(2));
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAConsumeExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAnOperationCanceledExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new OperationCanceledException("exception msg from test", innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAnExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    var innerEx = new Exception("exception msg from test");
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(innerEx);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var consumerCTS = new CancellationTokenSource();
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, consumerCTS);
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public void Subscribe_WithCancelToken_IfAConsumerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    var e = Assert.Throws<Exception>(() => sut.Subscribe(topics, this._handlerConsumerMock.Object));
    Assert.Equal("An instance of IConsumer was not provided in the inputs.", e.Message);
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfAConsumerCancellationTokenSourceWasNotProvidedInTheInputs_ItShouldCallConsumeFromTheConsumerInstanceAtLeastOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfATraceIdPathWasProvidedInTheInputs_ItShouldSetTheTraceIdInTheCurrentActivity()
  {
    Activity? createdActivity = null;

    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = activity =>
      {
        if (activity.Source.Name == activitySourceName && activity.DisplayName == this._kafkaInputs.ActivityName)
        {
          createdActivity = activity;
        }
      },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);

    var expectedTraceId = ActivityTraceId.CreateRandom().ToString();
    var consumeRes = new ConsumeResult<MyKey, MyValue>
    {
      Message = new Message<MyKey, MyValue>
      {
        Value = new MyValue
        {
          Id = ActivityTraceId.CreateRandom().ToString(),
          CorrelationId = expectedTraceId,
        },
      },
      Partition = new Partition(123),
      Offset = new Offset(987),
      Topic = "test topic name",
    };
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.ActivitySourceName = activitySourceName;
    this._kafkaInputs.ActivityName = "test an";
    this._kafkaInputs.TraceIdPath = "CorrelationId";

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(500);

    Assert.Equal(expectedTraceId, createdActivity.TraceId.ToString());
  }

  [Fact]
  public async Task Subscribe_WithCancelToken_IfATraceIdPathWasProvidedInTheInputs_IfTheTraceIdFoundAtTheProvidedPathWasNotValid_ItShouldGenerateALog()
  {
    Activity? createdActivity = null;

    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = activity =>
      {
        if (activity.Source.Name == activitySourceName && activity.DisplayName == this._kafkaInputs.ActivityName)
        {
          createdActivity = activity;
        }
      },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);

    var messageTraceId = Guid.NewGuid().ToString();
    var consumeRes = new ConsumeResult<MyKey, MyValue>
    {
      Message = new Message<MyKey, MyValue>
      {
        Value = new MyValue
        {
          Id = ActivityTraceId.CreateRandom().ToString(),
          CorrelationId = messageTraceId,
        },
      },
      Partition = new Partition(123),
      Offset = new Offset(987),
      Topic = "test topic name",
    };
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.ActivitySourceName = activitySourceName;
    this._kafkaInputs.ActivityName = "test an";
    this._kafkaInputs.TraceIdPath = "CorrelationId";

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object);
    await Task.Delay(500);

    this._loggerMock.Verify(m => m.Log(
      Microsoft.Extensions.Logging.LogLevel.Warning,
      null,
      $"The message received from the topic 'test topic name', partition '{123}' and offset '987' had an invalid value for a trace ID in the node 'CorrelationId': '{messageTraceId}'. The trace ID '{createdActivity.TraceId}' was used instead."
    ));
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallGetBoolFlagValueFromTheFeatureFlagInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_ItShouldCallTheHandlerReceivedAsInputAtLeastOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>
    {
      Message = new Message<MyKey, MyValue> { },
    };
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

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
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._ffMock.Verify(m => m.SubscribeToValueChanges("some ff key", It.IsAny<Action<FlagValueChangeEvent>?>()), Times.Once());
  }

  [Fact]
  public void Subscribe_WithFFKey_IfAFeatureFlagInstanceWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];

    var e = Assert.Throws<Exception>(() => sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key"));
    Assert.Equal("An instance of IFeatureFlags was not provided in the inputs.", e.Message);
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfAConsumeExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail), innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfAnOperationCanceledExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    var innerEx = new Exception();
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(new OperationCanceledException("exception msg from test", innerEx));

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfAnExceptionIsThrown_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    var innerEx = new Exception("exception msg from test");
    this._consumerMock.Setup(s => s.Consume(It.IsAny<CancellationToken>()))
      .Throws(innerEx);

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["some other test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    this._handlerConsumerMock.Verify(m => m(null, innerEx), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfATraceIdPathWasProvidedInTheInputs_ItShouldSetTheTraceIdInTheCurrentActivity()
  {
    Activity? createdActivity = null;

    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = activity =>
      {
        if (activity.Source.Name == activitySourceName && activity.DisplayName == this._kafkaInputs.ActivityName)
        {
          createdActivity = activity;
        }
      },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);

    var expectedTraceId = ActivityTraceId.CreateRandom().ToString();
    var consumeRes = new ConsumeResult<MyKey, MyValue>
    {
      Message = new Message<MyKey, MyValue>
      {
        Value = new MyValue
        {
          Id = ActivityTraceId.CreateRandom().ToString(),
          CorrelationId = expectedTraceId,
        },
      },
      Partition = new Partition(123),
      Offset = new Offset(987),
      Topic = "test topic name",
    };
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.ActivitySourceName = activitySourceName;
    this._kafkaInputs.ActivityName = "test an";
    this._kafkaInputs.TraceIdPath = "CorrelationId";

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(500);

    Assert.Equal(expectedTraceId, createdActivity.TraceId.ToString());
  }

  [Fact]
  public async Task Subscribe_WithFFKey_IfATraceIdPathWasProvidedInTheInputs_IfTheTraceIdFoundAtTheProvidedPathWasNotValid_ItShouldGenerateALog()
  {
    Activity? createdActivity = null;

    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = activity =>
      {
        if (activity.Source.Name == activitySourceName && activity.DisplayName == this._kafkaInputs.ActivityName)
        {
          createdActivity = activity;
        }
      },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);

    var messageTraceId = Guid.NewGuid().ToString();
    var consumeRes = new ConsumeResult<MyKey, MyValue>
    {
      Message = new Message<MyKey, MyValue>
      {
        Value = new MyValue
        {
          Id = ActivityTraceId.CreateRandom().ToString(),
          CorrelationId = messageTraceId,
        },
      },
      Partition = new Partition(123),
      Offset = new Offset(987),
      Topic = "test topic name",
    };
    this._consumerMock.SetupSequence(s => s.Consume(It.IsAny<CancellationToken>()))
      .Returns(consumeRes)
      .Throws(new OperationCanceledException());

    this._kafkaInputs.ActivitySourceName = activitySourceName;
    this._kafkaInputs.ActivityName = "test an";
    this._kafkaInputs.TraceIdPath = "CorrelationId";

    this._kafkaInputs.Consumer = this._consumerMock.Object;
    this._kafkaInputs.FeatureFlags = this._ffMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    IEnumerable<string> topics = ["test topic name"];
    sut.Subscribe(topics, this._handlerConsumerMock.Object, "some ff key");
    await Task.Delay(1000);

    this._loggerMock.Verify(m => m.Log(
      Microsoft.Extensions.Logging.LogLevel.Warning,
      null,
      $"The message received from the topic 'test topic name', partition '123' and offset '987' had an invalid value for a trace ID in the node 'CorrelationId': '{messageTraceId}'. The trace ID '{createdActivity.TraceId}' was used instead."
    ));
  }

  [Fact]
  public void Commit_ItShouldCallCommitFromTheConsumerInstanceOnceWithTheExpectedArguments()
  {
    this._kafkaInputs.Consumer = this._consumerMock.Object;
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var consumeRes = new ConsumeResult<MyKey, MyValue>();
    sut.Commit(consumeRes);

    this._consumerMock.Verify(m => m.Commit(consumeRes), Times.Once());
  }

  [Fact]
  public void Commit_IfAConsumerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new Kafka<MyKey, MyValue>(this._kafkaInputs);

    var e = Assert.Throws<Exception>(() => sut.Commit(new ConsumeResult<MyKey, MyValue>()));
    Assert.Equal("An instance of IConsumer was not provided in the inputs.", e.Message);
  }
}

public class MyKey
{
  public required string Id { get; set; }
}

public class MyValue
{
  public string? Id { get; set; }
  public string? Name { get; set; }
  public string? CorrelationId { get; set; }
  public List<MyValueDoc>? Docs { get; set; }
}

public class MyValueDoc
{
  public int? Count { get; set; }
  public string? Desc { get; set; }
}