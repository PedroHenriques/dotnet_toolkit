using System.Diagnostics;

namespace Toolkit.Types;

public struct LoggerInputs
{
  public required Microsoft.Extensions.Logging.ILogger logger;
  public required Activity activity;
}

public interface ILogger
{
  public void Log(Microsoft.Extensions.Logging.LogLevel level, Exception? ex, string message);
}