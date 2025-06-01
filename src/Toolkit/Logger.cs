using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Toolkit;

public partial class Logger : Types.ILogger
{
  private readonly Types.LoggerInputs _inputs;
  private readonly ILogger _logger;

  public Logger(Types.LoggerInputs inputs)
  {
    this._inputs = inputs;
    this._logger = inputs.logger;
  }

  [LoggerMessage(Message = "{message}")]
  public partial void Log(LogLevel level, Exception? ex, string message);

  public static Activity? SetTraceIds(
    string traceId, string activitySourceName, string activityName,
    string? spanId = null
  )
  {
    var activityTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
    var traceFlags = ActivityTraceFlags.Recorded;
    ActivitySpanId activitySpanId;
    if (String.IsNullOrEmpty(spanId))
    {
      activitySpanId = ActivitySpanId.CreateRandom();
    }
    else
    {
      activitySpanId = ActivitySpanId.CreateFromString(spanId.AsSpan());
    }

    var context = new ActivityContext(activityTraceId, activitySpanId, traceFlags);
    var source = new ActivitySource(activitySourceName);

    return source.StartActivity(activityName, ActivityKind.Internal, context);
  }
}