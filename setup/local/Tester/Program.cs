using System.Dynamic;
using Tester.Middlewares;
using Toolkit;
using Toolkit.Types;
using FFUtils = Toolkit.Utils.FeatureFlags;
using LoggerUtils = Toolkit.Utils.Logger;

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
Logger logger = new Logger(loggerInputs);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

app.UseMiddleware<TraceId>("x-trace-id", "Tester.API", "IncomingHttpRequest");

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

dynamic document = new ExpandoObject();
document.prop1 = "value 1";
document.prop2 = "value 2";
document.prop3 = true;
document.prop4 = 4.86;
document.prop5 = 10;

IFeatureFlags featureFlags = app.Services.GetService<IFeatureFlags>();

new Tester.Services.Mongodb(app, document, featureFlags, logger);
new Tester.Services.Redis(app, document);
new Tester.Services.Kafka(app, document, featureFlags);

logger.Log(LogLevel.Debug, null, "Tester: some debug message would go here.");
logger.Log(LogLevel.Information, null, "Tester: setup complete.");
logger.Log(LogLevel.Critical, new Exception("Tester: test exception for log"), "Tester: exception logging.");

app.Run();