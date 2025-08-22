using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Toolkit;

public partial class Logger : Types.ILogger
{
  private readonly Types.LoggerInputs _inputs;

  public Logger(Types.LoggerInputs inputs)
  {
    this._inputs = inputs;
  }

  public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> scope)
  {
    return this._inputs.logger.BeginScope(scope);
  }

  public void Log(LogLevel level, Exception? ex, string message, params object?[] args)
  {
    this._inputs.logger.Log(level, ex, message, args);
  }

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