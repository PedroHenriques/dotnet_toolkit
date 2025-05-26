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
}