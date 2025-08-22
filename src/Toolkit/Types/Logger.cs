using System.Diagnostics;

namespace Toolkit.Types;

public struct LoggerInputs
{
  public required Microsoft.Extensions.Logging.ILogger logger;
}

public interface ILogger
{
  public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> scope);

  public void Log(
    Microsoft.Extensions.Logging.LogLevel level, Exception? ex,
    string message, params object?[] args
  );

  public static virtual Activity? SetTraceIds(
    string traceId, string activitySourceName, string activityName,
    string? spanId = null
  ) => throw new NotImplementedException();
}