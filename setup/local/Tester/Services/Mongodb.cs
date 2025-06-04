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

  public Mongodb(WebApplication app, dynamic document, IFeatureFlags featureFlags, Toolkit.Types.ILogger logger)
  {
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

      await this._mongodb.CreateOneIndex<dynamic>(
        "myTestDb", "myTestCol", new BsonDocument { { "prop1", 1 } },
        new CreateIndexOptions { Name = "prop1_ASC" }
      );
      await this._mongodb.InsertOne<dynamic>("myTestDb", "myTestCol", document);

      logger.Log(LogLevel.Warning, null, "Finished processing the POST request to /mongo");

      return Results.Ok("Document inserted.");
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
      if (change.ChangeRecord != null)
      {
        using var activity = Logger.SetTraceIds(ActivityTraceId.CreateRandom().ToString(), "MongoDb Watcher", "Change received");
        logger.Log(LogLevel.Warning, null, "Received event from Mongo Stream:");
        logger.Log(LogLevel.Warning, null, JsonConvert.SerializeObject(change));
      }
    }

    logger.Log(LogLevel.Information, null, "Stopped listening to Mongo Stream.");
  }
}