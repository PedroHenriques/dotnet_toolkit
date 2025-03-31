using System.Dynamic;
using Toolkit;
using Toolkit.Types;
using FFUtils = Toolkit.Utils.FeatureFlags;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

dynamic document = new ExpandoObject();
document.prop1 = "value 1";
document.prop2 = "value 2";

var ffInputs = FFUtils.PrepareInputs(
  Environment.GetEnvironmentVariable("LD_ENV_SDK_KEY") ?? "",
  Environment.GetEnvironmentVariable("LD_CONTEXT_API_KEY") ?? "",
  Environment.GetEnvironmentVariable("LD_CONTEXT_NAME") ?? "",
  EnvNames.dev
);
var featureFlags = new FeatureFlags(ffInputs);

new Tester.Services.Mongodb(app, document, featureFlags);
new Tester.Services.Redis(app, document);
new Tester.Services.Kafka(app, document, featureFlags);

app.Run();