using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Toolkit.Asp.Middlewares;

[ExcludeFromCodeCoverage(Justification = "Not unit testable since the Activities generated will not be available in the unit test context.")]
public class TraceIdMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger _logger;
  private readonly string _traceIdHeader;
  private readonly string _activitySourceName;
  private readonly string _activityName;

  public TraceIdMiddleware(
    RequestDelegate next, ILogger<TraceIdMiddleware> logger, string traceIdHeader,
    string activitySourceName, string activityName
  )
  {
    this._next = next;
    this._logger = logger;
    this._traceIdHeader = traceIdHeader;
    this._activitySourceName = activitySourceName;
    this._activityName = activityName;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    string? traceId = context.Request.Headers[_traceIdHeader];

    Activity? activity = Activity.Current;

    if (string.IsNullOrWhiteSpace(traceId))
    {
      if (activity != null)
      {
        traceId = activity.TraceId.ToString();
      }
      else
      {
        traceId = ActivityTraceId.CreateRandom().ToString();
      }
      activity = Logger.SetTraceIds(traceId, _activitySourceName, _activityName);

      this._logger.Log(
        LogLevel.Warning,
        "No trace ID provided with the request. Using the generated trace id: {traceId}",
        traceId
      );
    }
    else
    {
      activity = Logger.SetTraceIds(traceId, _activitySourceName, _activityName);

      this._logger.Log(
        LogLevel.Information,
        "Trace ID - {traceId} - provided with the request. Using it in the logs of this request.",
        traceId
      );
    }

    try
    {
      await _next(context);
    }
    finally
    {
      if (activity != null)
      {
        activity.Dispose();
      }
    }
  }
}
