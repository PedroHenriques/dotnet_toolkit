using TKMongo = Toolkit.Mongodb;
using MongodbUtils = Toolkit.Utils.Mongodb;
using MongoDB.Bson;
using MongoDB.Driver;
using Toolkit.Types;
using Newtonsoft.Json;
using Toolkit;
using System.Diagnostics;

namespace Tester.Services;

class Mongodb
{
  private readonly IMongodb _mongodb;
  private readonly IFeatureFlags _ff;

  public Mongodb(WebApplication app, MyValue document, IFeatureFlags featureFlags, Toolkit.Types.ILogger logger)
  {
    logger.BeginScope(
      new Dictionary<string, object?>
      {
        ["scope.prop"] = "test mongo prop",
        ["hello from scope"] = "world from mongo",
      }
    );
    logger.Log(LogLevel.Warning, null, "Test message with scope and structured attributes '{someMongoProp}' from the Kafka service", "some mongo prop");

    string? mongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR");
    if (mongoConStr == null)
    {
      throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");
    }
    MongoDbInputs mongodbInputs = MongodbUtils.PrepareInputs(mongoConStr);
    this._mongodb = new TKMongo(mongodbInputs);

    app.MapPost("/mongo", async () =>
    {
      logger.Log(LogLevel.Warning, null, "Started processing the POST request to /mongo");

      var mongoDoc = new MyValueMongo
      {
        Prop1 = document.Prop1,
        Prop2 = document.Prop2,
        Prop3 = document.Prop3,
        Prop4 = document.Prop4,
        Prop5 = document.Prop5,
      };

      await this._mongodb.CreateOneIndex<dynamic>(
        "myTestDb", "myTestCol", new BsonDocument { { "prop1", 1 } },
        new CreateIndexOptions { Name = "prop1_ASC" }
      );
      await this._mongodb.InsertOne<MyValueMongo>("myTestDb", "myTestCol", mongoDoc);

      logger.Log(LogLevel.Warning, null, "Finished processing the POST request to /mongo");

      return Results.Ok("Document inserted.");
    });

    app.MapGet("/mongo/unique", async () =>
    {
      logger.Log(LogLevel.Information, null, "Started processing the GET request to /mongo/unique");

      var res = await this._mongodb.Find<MyValueMongo>(
        "myTestDb", "myTestCol", 1, 10, null, false, null, "prop4"
      );

      logger.Log(LogLevel.Information, null, "Finished processing the GET request to /mongo/unique");

      return TypedResults.Ok(res);
    });

    app.MapGet("/mongo", async () =>
    {
      logger.Log(LogLevel.Information, null, "Started processing the GET request to /mongo");

      var res = await this._mongodb.Find<MyValueMongo>(
        "myTestDb", "myTestCol", 1, 10, null, false, null
      );

      logger.Log(LogLevel.Information, null, "Finished processing the GET request to /mongo");

      return TypedResults.Ok(res);
    });

    app.MapGet("/mongo/counter", async () =>
    {
      logger.Log(LogLevel.Information, null, "Started processing the GET request to /mongo/counter");

      var res = await this._mongodb.Counter(
        "myTestDb", "myTestCol", 1, 10, "prop4", "prop1", false, true, null, null
      );

      logger.Log(LogLevel.Information, null, "Finished processing the GET request to /mongo/counter");

      return TypedResults.Ok(res);
    });

    this._ff = featureFlags;
    string ffKey = "ctt-net-toolkit-tester-consume-kafka-events";
    CancellationTokenSource cts = new CancellationTokenSource();

    featureFlags.SubscribeToValueChanges(
      ffKey,
      (ev) =>
      {
        logger.Log(LogLevel.Information, null, "Received new feature flag value");
        if (ev.NewValue.AsBool)
        {
          cts = new CancellationTokenSource();
          WatchDb(cts.Token, logger);
        }
        else
        {
          cts.Cancel();
        }
      }
    );

    if (featureFlags.GetBoolFlagValue(ffKey))
    {
      WatchDb(cts.Token, logger);
    }
  }

  private async void WatchDb(CancellationToken token, Toolkit.Types.ILogger logger)
  {
    logger.Log(LogLevel.Information, null, "Started listening to Mongo Stream.");

    await foreach (WatchData change in this._mongodb.WatchDb("myTestDb", null, token))
    {
      using var activity = Logger.SetTraceIds(ActivityTraceId.CreateRandom().ToString(), "MongoDb Watcher", "Change received");
      logger.Log(LogLevel.Information, null, "Received event from Mongo Stream:");
      logger.Log(LogLevel.Information, null, JsonConvert.SerializeObject(change));
    }

    logger.Log(LogLevel.Information, null, "Stopped listening to Mongo Stream.");
  }
}