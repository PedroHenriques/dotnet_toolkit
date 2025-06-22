using System.Dynamic;
using Toolkit.Asp.Middlewares;
using Toolkit;
using Toolkit.Types;
using FFUtils = Toolkit.Utils.FeatureFlags;
using LoggerUtils = Toolkit.Utils.Logger;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IFeatureFlags>(sp =>
{
  FeatureFlagsInputs ffInputs = FFUtils.PrepareInputs(
    Environment.GetEnvironmentVariable("LD_ENV_SDK_KEY") ?? "",
    Environment.GetEnvironmentVariable("LD_CONTEXT_API_KEY") ?? "",
    Environment.GetEnvironmentVariable("LD_CONTEXT_NAME") ?? "",
    EnvNames.dev
  );

  return new FeatureFlags(ffInputs);
});

// Setup the host logger
LoggerUtils.PrepareInputs(builder);

// Create a standalone logger
var loggerInputs = LoggerUtils.PrepareInputs("Tester.Program", "Tester", "Main thread");
Toolkit.Types.ILogger logger = new Logger(loggerInputs);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

MyValue document = new MyValue
{
  Prop1 = "value 1",
  Prop2 = "value 2",
  Prop3 = true,
  Prop4 = 4.86,
  Prop5 = 10,
};

IFeatureFlags featureFlags = app.Services.GetService<IFeatureFlags>();

featureFlags.GetBoolFlagValue("ctt-net-toolkit-tester-consume-kafka-events");
featureFlags.SubscribeToValueChanges("ctt-net-toolkit-tester-consume-kafka-events");

app.UseMiddleware<CheckApiActiveMiddleware>("ctt-net-toolkit-tester-consume-kafka-events");
app.UseMiddleware<TraceIdMiddleware>("x-trace-id", "Tester.API", "IncomingHttpRequest");

new Tester.Services.Mongodb(app, document, featureFlags, logger);
new Tester.Services.Redis(app, document);
new Tester.Services.Kafka(app, document, featureFlags);

logger.Log(LogLevel.Debug, null, "Tester: some debug message would go here.");
logger.Log(LogLevel.Information, null, "Tester: setup complete.");
logger.Log(LogLevel.Critical, new Exception("Tester: test exception for log"), "Tester: exception logging.");

app.Run();

public class MyKey
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public string Id { get; set; }
}

public class MyValue
{
  [JsonPropertyName("prop1")]
  [JsonProperty("prop1")]
  public string Prop1 { get; set; }

  [JsonPropertyName("prop2")]
  [JsonProperty("prop2")]
  public string Prop2 { get; set; }

  [JsonPropertyName("prop3")]
  [JsonProperty("prop3")]
  public bool Prop3 { get; set; }

  [JsonPropertyName("prop4")]
  [JsonProperty("prop4")]
  public double Prop4 { get; set; }

  [JsonPropertyName("prop5")]
  [JsonProperty("prop5")]
  public int Prop5 { get; set; }
}