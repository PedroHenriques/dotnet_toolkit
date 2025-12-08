using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class LoggerTests : IDisposable
{
  private const string ACTIVITY_SOURCE_NAME = "test activity source - logger";
  private readonly Mock<Microsoft.Extensions.Logging.ILogger> _loggerMock;
  private readonly Mock<IDisposable> _disposableMock;
  private readonly LoggerInputs _loggerInputs;

  public LoggerTests()
  {
    this._loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger>(MockBehavior.Strict);
    this._disposableMock = new Mock<IDisposable>(MockBehavior.Strict);

    this._loggerMock.Setup(s => s.Log<It.IsAnyType>(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
    this._loggerMock.Setup(s => s.BeginScope<It.IsAnyType>(It.IsAny<It.IsAnyType>()))
      .Returns(this._disposableMock.Object);

    this._loggerInputs = new LoggerInputs
    {
      logger = this._loggerMock.Object,
    };
  }

  public void Dispose()
  {
    this._loggerMock.Reset();
    this._disposableMock.Reset();
  }

  [Fact]
  public void BeginScope_ItShouldCallBeginScopeFromTheDotNetLoggerOnceWithTheExpectedArguments()
  {
    var testScope = new Dictionary<string, object?> { };

    var sut = new Logger(this._loggerInputs);
    sut.BeginScope(testScope);

    this._loggerMock.Verify(m => m.BeginScope<IReadOnlyDictionary<string, object?>>(testScope));
  }

  [Fact]
  public void Log_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedLogLevel()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, null, "hello world");

    var actualLogLevel = this._loggerMock.Invocations[0].Arguments[0];
    Assert.Equal(LogLevel.Debug, actualLogLevel);
  }

  [Fact]
  public void Log_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedMessage()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, null, "hello world");

    var actualMsg = this._loggerMock.Invocations[0].Arguments[2];
    Assert.Equal("hello world", actualMsg.ToString());
  }

  [Fact]
  public void Log_IfPlaceholdersAreProvided_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedMessage()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, null, "hello world {a} {b}", "abc", "def");

    var actualMsg = this._loggerMock.Invocations[0].Arguments[2];
    Assert.Equal("hello world abc def", actualMsg.ToString());
  }

  [Fact]
  public void Log_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedException()
  {
    var testEx = new Exception();

    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, testEx, "hello world");

    var actualEx = this._loggerMock.Invocations[0].Arguments[3];
    Assert.Equal(testEx, actualEx);
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivity()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "another test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName);
    Assert.IsType<Activity>(activity);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheProvidedTraceId()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "yet another test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName);
    Assert.Equal(testTraceId, activity.TraceId);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheProvidedActivitySourceName()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "some test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName);
    Assert.Equal(ACTIVITY_SOURCE_NAME, activity.Source.Name);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheProvidedActivityName()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "test activity name";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName);
    Assert.Equal(testActivityName, activity.DisplayName);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheExpectedTraceFlags()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "test activity name";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName);
    Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_IfASpanIdIsProvided_ItShouldReturnAnActivityWithTheProvidedSpanId()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testSpanId = ActivitySpanId.CreateRandom();
    var testActivityName = "test activity name";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName, testSpanId.ToString());
    Assert.Equal(testSpanId, activity.ParentSpanId);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_IfTheProvidedTraceIdIsNotValid_ItShouldReturnAnActivityWithARandomlyGeneratedTraceId()
  {
    // We need to have an activity listener for new activities to be created and registered
    var source = new ActivitySource(ACTIVITY_SOURCE_NAME);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == ACTIVITY_SOURCE_NAME,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = Guid.NewGuid();
    var testActivityName = "yet 1 more test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), ACTIVITY_SOURCE_NAME, testActivityName);
    Assert.NotEqual(testTraceId.ToString(), activity.TraceId.ToString());
    activity.Dispose();
  }
}
