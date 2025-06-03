using System.Diagnostics;
using Toolkit;

namespace Tester.Middlewares;

public class TraceId
{
  private readonly RequestDelegate _next;
  private readonly string _traceIdHeader;
  private readonly string _activitySourceName;
  private readonly string _activityName;

  public TraceId(RequestDelegate next, string traceIdHeader, string activitySourceName, string activityName)
  {
    this._next = next;
    this._traceIdHeader = traceIdHeader;
    this._activitySourceName = activitySourceName;
    this._activityName = activityName;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    string? traceId = context.Request.Headers[_traceIdHeader];

    Activity? activity = null;

    if (string.IsNullOrWhiteSpace(traceId) == false)
    {
      activity = Logger.SetTraceIds(traceId, _activitySourceName, _activityName);
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
