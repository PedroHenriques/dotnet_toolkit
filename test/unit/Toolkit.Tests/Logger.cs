using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class LoggerTests : IDisposable
{
  private readonly Mock<Microsoft.Extensions.Logging.ILogger> _loggerMock;
  private readonly LoggerInputs _loggerInputs;

  public LoggerTests()
  {
    this._loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger>(MockBehavior.Strict);

    this._loggerMock.Setup(s => s.IsEnabled(It.IsAny<LogLevel>()))
      .Returns(true);
    this._loggerMock.Setup(s => s.Log<It.IsAnyType>(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

    this._loggerInputs = new LoggerInputs
    {
      logger = this._loggerMock.Object,
    };
  }

  public void Dispose()
  {
    this._loggerMock.Reset();
  }

  [Fact]
  public void Log_ItShouldCallIsEnabledFromTheDotNetLoggerOnceWithTheProvidedLoglevel()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Information, null, "hello");

    this._loggerMock.Verify(m => m.IsEnabled(LogLevel.Information), Times.Once());
  }

  [Fact]
  public void Log_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedLogLevel()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, null, "hello world");

    var actualLogLevel = this._loggerMock.Invocations[1].Arguments[0];
    Assert.Equal(LogLevel.Debug, actualLogLevel);
  }

  [Fact]
  public void Log_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedMessage()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, null, "hello world");

    var actualMsg = this._loggerMock.Invocations[1].Arguments[2];
    Assert.Equal("hello world", actualMsg.ToString());
  }

  [Fact]
  public void Log_ItShouldCallLogFromTheDotNetLoggerOnceWithTheExpectedException()
  {
    var sut = new Logger(this._loggerInputs);
    sut.Log(LogLevel.Debug, null, "hello world");

    var actualEx = this._loggerMock.Invocations[1].Arguments[3];
    Assert.Null(actualEx);
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivity()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "another test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), activitySourceName, testActivityName);
    Assert.IsType<Activity>(activity);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheProvidedTraceId()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "yet another test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), activitySourceName, testActivityName);
    Assert.Equal(testTraceId, activity.TraceId);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheProvidedActivitySourceName()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "some test";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), activitySourceName, testActivityName);
    Assert.Equal(activitySourceName, activity.Source.Name);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheProvidedActivityName()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "test activity name";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), activitySourceName, testActivityName);
    Assert.Equal(testActivityName, activity.DisplayName);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_ItShouldReturnAnActivityWithTheExpectedTraceFlags()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testActivityName = "test activity name";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), activitySourceName, testActivityName);
    Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
    activity.Dispose();
  }

  [Fact]
  public void SetTraceIds_IfASpanIdIsProvided_ItShouldReturnAnActivityWithTheProvidedSpanId()
  {
    // We need to have an activity listener for new activities to be created and registered
    var activitySourceName = "test activity source";
    var source = new ActivitySource(activitySourceName);
    var listener = new ActivityListener()
    {
      ShouldListenTo = s => s.Name == activitySourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
      ActivityStarted = _ => { },
      ActivityStopped = _ => { }
    };
    ActivitySource.AddActivityListener(listener);
    source.StartActivity("testActivity", ActivityKind.Internal);

    var testTraceId = ActivityTraceId.CreateRandom();
    var testSpanId = ActivitySpanId.CreateRandom();
    var testActivityName = "test activity name";
    var activity = Logger.SetTraceIds(testTraceId.ToString(), activitySourceName, testActivityName, testSpanId.ToString());
    Assert.Equal(testSpanId, activity.ParentSpanId);
    activity.Dispose();
  }
}