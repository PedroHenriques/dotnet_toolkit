using System.Diagnostics;

namespace Toolkit.Types;

public struct LoggerInputs
{
  public required Microsoft.Extensions.Logging.ILogger logger;
}

public interface ILogger
{
  public void Log(Microsoft.Extensions.Logging.LogLevel level, Exception? ex, string message);

  public static virtual Activity? SetTraceIds(
    string traceId, string activitySourceName, string activityName,
    string? spanId = null
  ) => throw new NotImplementedException();
}