using System.Dynamic;
using MongoDB.Driver;
using StackExchange.Redis;
using Confluent.Kafka;
using Toolkit;
using EventBusUtils = Toolkit.Utils.EventBus<string, dynamic>;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

string? mongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR");
if (mongoConStr == null)
{
  throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");
}
MongoClient? mongoClient = new MongoClient(mongoConStr);
if (mongoClient == null)
{
  throw new Exception("Mongo Client returned NULL.");
}
var db = new Db(mongoClient);

string? redisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR");
if (redisConStr == null)
{
  throw new Exception("Could not get the 'REDIS_CON_STR' environment variable");
}
ConfigurationOptions redisConOpts = new ConfigurationOptions
{
  EndPoints = { redisConStr },
};
IConnectionMultiplexer? redisClient = ConnectionMultiplexer.Connect(redisConOpts);
if (redisClient == null)
{
  throw new Exception("Redis Client returned NULL.");
}
var cache = new Cache(redisClient);

string? schemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL");
if (schemaRegistryUrl == null)
{
  throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");
}
var schemaRegistryConfig = new SchemaRegistryConfig { Url = schemaRegistryUrl };
ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

string? kafkaConStr = Environment.GetEnvironmentVariable("KAFKA_CON_STR");
if (kafkaConStr == null)
{
  throw new Exception("Could not get the 'KAFKA_CON_STR' environment variable");
}
var producerConfig = new ProducerConfig
{
  BootstrapServers = kafkaConStr,
};
var producerBuilder = new ProducerBuilder<string, dynamic>(producerConfig);

var consumerConfig = new ConsumerConfig
{
  BootstrapServers = kafkaConStr,
  GroupId = "example-consumer-group",
};
var consumerBuilder = new ConsumerBuilder<string, dynamic>(consumerConfig);

var eventBusInputs = EventBusUtils.PrepareInputs(
  schemaRegistry, "myTestTopic-value", 1, new JsonSerializer<dynamic>(schemaRegistry),
  producerBuilder, consumerBuilder
);
var eventBus = new EventBus<string, dynamic>(eventBusInputs);

dynamic document = new ExpandoObject();
document.prop1 = "value 1";
document.prop2 = "value 2";

app.MapPost("/mongo", async () =>
{
  await db.InsertOne<dynamic>("myTestDb", "myTestCol", document);

  return Results.Ok("Document inserted.");
});

app.MapPost("/redis", async () =>
{
  var res1 = await cache.Set("prop1", document.prop1);
  Console.WriteLine($"Insert of prop1 key: {res1}");
  var res2 = await cache.Set("prop2", document.prop2);
  Console.WriteLine($"Insert of prop2 key: {res2}");

  return Results.Ok("Keys inserted.");
});

app.MapPost("/kafka", () =>
{
  eventBus.Publish(
    "myTestTopic",
    new Message<string, dynamic> { Key = DateTime.UtcNow.ToString(), Value = document },
    (res) => { Results.Ok("Event inserted."); }
  );
});

Task.Run(() =>
{
  eventBus.Subscribe(
    ["myTestTopic"],
    (res) => { Console.WriteLine(res.Message.Value); }
  );
});

app.Run();