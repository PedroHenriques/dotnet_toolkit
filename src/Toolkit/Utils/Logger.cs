using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the OpenTelemetry SDK.")]
public static class Logger
{
  private static ILoggerFactory? _factory;

  public static IHostApplicationBuilder PrepareInputs(IHostApplicationBuilder builder)
  {
    SetupBuilder(builder.Logging);

    return builder;
  }

  public static LoggerInputs PrepareInputs(string logCategory)
  {
    _factory = LoggerFactory.Create(builder =>
    {
      SetupBuilder(builder);
    });
    Microsoft.Extensions.Logging.ILogger logger = _factory.CreateLogger(logCategory);

    AppDomain.CurrentDomain.ProcessExit += (_, __) => Dispose(logger);
    Console.CancelKeyPress += (_, e) =>
    {
      e.Cancel = true;
      Dispose(logger);
    };

    return new LoggerInputs
    {
      logger = logger,
    };
  }

  private static void Dispose(Microsoft.Extensions.Logging.ILogger logger)
  {
    logger.Log(LogLevel.Debug, ".Net Toolkit: Logger.Dispose() called.");
    if (_factory != null)
    {
      _factory.Dispose();
    }
  }

  private static void SetupBuilder(ILoggingBuilder builder)
  {
    string? logDestHost = Environment.GetEnvironmentVariable("LOG_DESTINATION_HOST");
    if (string.IsNullOrWhiteSpace(logDestHost))
    {
      throw new Exception("❌ ERROR: LOG_DESTINATION_HOST is not set!");
    }

    string? logDestPort = Environment.GetEnvironmentVariable("LOG_DESTINATION_PORT");
    if (string.IsNullOrWhiteSpace(logDestPort))
    {
      throw new Exception("❌ ERROR: LOG_DESTINATION_PORT is not set!");
    }

    string? serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME");
    if (string.IsNullOrWhiteSpace(serviceName))
    {
      serviceName = "N/A";
      Console.WriteLine("⚠️ WARNING: SERVICE_NAME is not set!");
    }

    string? serviceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION");
    if (string.IsNullOrWhiteSpace(serviceVersion))
    {
      serviceVersion = "N/A";
      Console.WriteLine("⚠️ WARNING: SERVICE_VERSION is not set!");
    }

    string? projectName = Environment.GetEnvironmentVariable("PROJECT_NAME");
    if (string.IsNullOrWhiteSpace(projectName))
    {
      projectName = "N/A";
      Console.WriteLine("⚠️ WARNING: PROJECT_NAME is not set!");
    }

    string? deploymentEnv = Environment.GetEnvironmentVariable("DEPLOYMENT_ENV");
    if (string.IsNullOrWhiteSpace(deploymentEnv))
    {
      deploymentEnv = "N/A";
      Console.WriteLine("⚠️ WARNING: DEPLOYMENT_ENV is not set!");
    }

    var resourceBuilder = CreateResourceBuilder(
      serviceName, serviceVersion, projectName, deploymentEnv
    );
    var builtResource = resourceBuilder.Build();

    builder.SetMinimumLevel(GetMinLogLevel());
    builder.AddOpenTelemetry(options =>
    {
      options.IncludeFormattedMessage = true;
      options.IncludeScopes = true;
      options.SetResourceBuilder(resourceBuilder);

      if (deploymentEnv == "local")
      {
        options.AddConsoleExporter();
      }
      else
      {
        options.AddProcessor(new SimpleLogRecordExportProcessor(
          new LogstashTcpLogExporter(logDestHost, int.Parse(logDestPort), builtResource)
        ));
      }
    });
  }

  private static ResourceBuilder CreateResourceBuilder(
    string serviceName, string serviceVersion, string projectName, string deploymentEnv
  )
  {
    return ResourceBuilder.CreateDefault()
      .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
      .AddAttributes(new[]
      {
        new KeyValuePair<string, object>("project.name", projectName),
        new KeyValuePair<string, object>("deployment.environment", deploymentEnv),
      });
  }

  private static LogLevel GetMinLogLevel()
  {
    Dictionary<string, LogLevel> levels = new Dictionary<string, LogLevel>
    {
      { "trace", LogLevel.Trace },
      { "debug", LogLevel.Debug },
      { "information", LogLevel.Information },
      { "warning", LogLevel.Warning },
      { "error", LogLevel.Error },
      { "critical", LogLevel.Critical },
    };

    string desiredLevel = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "warning";

    LogLevel minLevel;
    if (levels.TryGetValue(desiredLevel, out minLevel) == false)
    {
      throw new Exception($"The desired minimum log level '{desiredLevel}' is not valid.");
    }

    return minLevel;
  }
}