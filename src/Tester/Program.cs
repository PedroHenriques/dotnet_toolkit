using System.Dynamic;
using MongoDB.Driver;
using StackExchange.Redis;
using Confluent.Kafka;
using Toolkit;
using KafkaUtils = Toolkit.Utils.Kafka<string, dynamic>;
using Confluent.SchemaRegistry;
using ffUtils = Toolkit.Utils.FeatureFlags;

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
var mongoDb = new Mongodb(mongoClient);

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
var redis = new Redis(redisClient);

string? schemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL");
if (schemaRegistryUrl == null)
{
  throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");
}
var schemaRegistryConfig = new SchemaRegistryConfig { Url = schemaRegistryUrl };

string? kafkaConStr = Environment.GetEnvironmentVariable("KAFKA_CON_STR");
if (kafkaConStr == null)
{
  throw new Exception("Could not get the 'KAFKA_CON_STR' environment variable");
}
var producerConfig = new ProducerConfig
{
  BootstrapServers = kafkaConStr,
};
var consumerConfig = new ConsumerConfig
{
  BootstrapServers = kafkaConStr,
  GroupId = "example-consumer-group",
  AutoOffsetReset = AutoOffsetReset.Latest,
  EnableAutoCommit = false,
};

var eventBusInputs = KafkaUtils.PrepareInputs(
  schemaRegistryConfig, "myTestTopic-value", 1, producerConfig, consumerConfig
);
var kafka = new Kafka<string, dynamic>(eventBusInputs);

dynamic document = new ExpandoObject();
document.prop1 = "value 1";
document.prop2 = "value 2";

app.MapPost("/mongo", async () =>
{
  await mongoDb.InsertOne<dynamic>("myTestDb", "myTestCol", document);

  return Results.Ok("Document inserted.");
});

app.MapPost("/redis", async () =>
{
  var res1 = await redis.Set("prop1", document.prop1);
  var res2 = await redis.Set("prop2", document.prop2);

  return Results.Ok("Keys inserted.");
});

app.MapPost("/kafka", () =>
{
  kafka.Publish(
    "myTestTopic",
    new Message<string, dynamic> { Key = DateTime.UtcNow.ToString(), Value = document },
    (res) => { Console.WriteLine("Event inserted."); }
  );
});

var ffInputs = ffUtils.PrepareInputs(
  "sdk-ca6ad97c-9c73-4a46-bfda-7d0ba2e0ae11",
  "api-7daf94ee-a809-438e-9a1f-f280ef224217",
  "CTT .Net Toolkit"
);
var featureFlags = new FeatureFlags(ffInputs);

string ffKey = "ctt-net-toolkit-tester-consume-kafka-events";

CancellationTokenSource? cts = null;
var subToTopic = () =>
{
  Console.WriteLine("Subscribing to Kafka topic.");

  cts = new CancellationTokenSource();
  kafka.Subscribe(
    ["myTestTopic"],
    (res) =>
    {
      Console.WriteLine($"Processing event from partition: {res.Partition} | offset: {res.Offset}");
      Console.WriteLine(res.Message.Value);
      kafka.Commit(res);
    },
    cts
  );
};

var unsubToTopic = () =>
{
  if (cts == null) { return; }

  Console.WriteLine("Un-Subscribing to Kafka topic.");
  cts.Cancel();
};

featureFlags.SubscribeToValueChanges(
  ffKey,
  (ev) =>
  {
    if (ev.NewValue.AsBool)
    {
      subToTopic();
    }
    else
    {
      unsubToTopic();
    }
  }
);

if (featureFlags.GetBoolFlagValue(ffKey))
{
  subToTopic();
}

app.Run();