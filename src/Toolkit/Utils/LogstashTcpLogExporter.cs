using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

[ExcludeFromCodeCoverage(Justification = "Temporary class untill we have the Opentelemetry log collector available.")]
public class LogstashTcpLogExporter : BaseExporter<LogRecord>
{
  private readonly string _host;
  private readonly int _port;
  private readonly Resource _resource;
  private TcpClient? _client;
  private StreamWriter? _writer;

  public LogstashTcpLogExporter(string host, int port, Resource resource)
  {
    this._host = host;
    this._port = port;
    this._resource = resource;
    Connect();
  }

  private void Connect()
  {
    this._client = new TcpClient();
    this._client.Connect(this._host, this._port);

    var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    this._writer = new StreamWriter(this._client.GetStream(), encoding)
    {
      AutoFlush = true
    };
  }

  public override ExportResult Export(in Batch<LogRecord> batch)
  {
    if (this._writer == null) { return ExportResult.Failure; }

    foreach (var record in batch)
    {
      var logEntry = new Dictionary<string, object?>
      {
        ["timestamp"] = record.Timestamp.ToUniversalTime().ToString("o"),
        ["log_level"] = record.LogLevel.ToString(),
        ["log_level_int"] = (int)record.LogLevel,
        ["event_id"] = record.EventId.Id,
        ["category"] = record.CategoryName,
        ["message"] = record.FormattedMessage ?? record.Body?.ToString(),
        ["trace_id"] = record.TraceId.ToHexString(),
        ["span_id"] = record.SpanId.ToHexString(),
      };

      if (record.Exception != null)
      {
        logEntry["exception"] = record.Exception.ToString();
      }

      if (!string.IsNullOrWhiteSpace(record.EventId.Name))
      {
        logEntry["event_name"] = record.EventId.Name;
      }

      if (record.Attributes != null)
      {
        foreach (var attr in record.Attributes)
        {
          if (attr.Value == null) { continue; }
          logEntry[$"attr.{attr.Key}"] = attr.Value.ToString();
        }
      }

      if (this._resource != null)
      {
        foreach (var kvp in this._resource.Attributes)
        {
          logEntry[$"resource.{kvp.Key}"] = kvp.Value.ToString();
        }
      }

      var json = JsonSerializer.Serialize(logEntry);
      this._writer.WriteLine(json);
    }

    return ExportResult.Success;
  }

  protected override bool OnShutdown(int timeoutMilliseconds)
  {
    if (this._writer != null)
    {
      this._writer.Dispose();
    }
    if (this._client != null)
    {
      this._client.Close();
    }
    return true;
  }

  protected override bool OnForceFlush(int timeoutMilliseconds) => true;
}
