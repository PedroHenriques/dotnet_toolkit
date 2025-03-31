using TKMongo = Toolkit.Mongodb;
using MongodbUtils = Toolkit.Utils.Mongodb;
using MongoDB.Bson;
using MongoDB.Driver;
using Toolkit.Types;
using Newtonsoft.Json;

namespace Tester.Services;

class Mongodb
{
  private readonly IMongodb _mongodb;
  private readonly IFeatureFlags _ff;

  public Mongodb(WebApplication app, dynamic document, IFeatureFlags featureFlags)
  {
    string? mongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR");
    if (mongoConStr == null)
    {
      throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");
    }
    var mongodbInputs = MongodbUtils.PrepareInputs(mongoConStr);
    this._mongodb = new TKMongo(mongodbInputs);

    app.MapPost("/mongo", async () =>
    {
      await this._mongodb.CreateOneIndex<dynamic>(
        "myTestDb", "myTestCol", new BsonDocument { { "prop1", 1 } },
        new CreateIndexOptions { Name = "prop1_ASC" }
      );
      await this._mongodb.InsertOne<dynamic>("myTestDb", "myTestCol", document);

      return Results.Ok("Document inserted.");
    });

    this._ff = featureFlags;
    string ffKey = "ctt-net-toolkit-tester-consume-kafka-events";
    CancellationTokenSource cts = new CancellationTokenSource();

    featureFlags.SubscribeToValueChanges(
      ffKey,
      (ev) =>
      {
        if (ev.NewValue.AsBool)
        {
          cts = new CancellationTokenSource();
          WatchDb(cts.Token);
        }
        else
        {
          cts.Cancel();
        }
      }
    );

    if (featureFlags.GetBoolFlagValue(ffKey))
    {
      WatchDb(cts.Token);
    }
  }

  private async void WatchDb(CancellationToken token)
  {
    Console.WriteLine("Started listening to Mongo Stream.");

    await foreach (WatchData change in this._mongodb.WatchDb("myTestDb", null, token))
    {
      if (change.ChangeRecord != null)
      {
        Console.WriteLine("Received event from Mongo Stream:");
        Console.WriteLine(JsonConvert.SerializeObject(change));
      }
    }

    Console.WriteLine("Stopped listening to Mongo Stream.");
  }
}