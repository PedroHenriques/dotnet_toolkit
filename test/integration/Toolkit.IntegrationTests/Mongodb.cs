using System.Text.Json.Serialization;
using DbFixtures.Mongodb;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using Toolkit.Types;
using MongoDbUtils = Toolkit.Utils.Mongodb;

namespace Toolkit.Tests.Integration;

[Trait("Type", "Integration")]
public class MongodbTests : IDisposable
{
  private const string DB_NAME = "testDb";
  private const string COLL_NAME = "testCol";
  private readonly IMongoClient _client;
  private readonly DbFixtures.DbFixtures _dbFixtures;
  private readonly IMongodb _sut;

  public MongodbTests()
  {
    string connStr = "mongodb://admin:pw@mongodb:27017/admin?authMechanism=SCRAM-SHA-256&replicaSet=rs0";
    this._client = new MongoClient(connStr);
    var driver = new MongodbDriver(this._client, DB_NAME);
    this._dbFixtures = new DbFixtures.DbFixtures([driver]);

    this._sut = new Mongodb(MongoDbUtils.PrepareInputs(connStr));
  }

  public void Dispose()
  {
    this._dbFixtures.CloseDrivers();
  }

  [Fact]
  public async Task InsertOne_ItShouldInsertTheDocumentInTheCollection()
  {
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, [] }
      }
    );

    var expectedDoc = new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" };
    await this._sut.InsertOne<TestDoc>(DB_NAME, COLL_NAME, expectedDoc);

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);
    var docs = await coll.Find(FilterDefinition<TestDoc>.Empty).ToListAsync();

    Assert.Equal(
      JsonConvert.SerializeObject(new TestDoc[] { expectedDoc }),
      JsonConvert.SerializeObject(docs)
    );
  }

  [Fact]
  public async Task InsertMany_ItShouldInsertTheDocumentsInTheCollection()
  {
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, [] }
      }
    );

    TestDoc[] expectedDocs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
    ];
    await this._sut.InsertMany<TestDoc>(DB_NAME, COLL_NAME, expectedDocs);

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);
    var docs = await coll.Find(FilterDefinition<TestDoc>.Empty).ToListAsync();

    Assert.Equal(
      JsonConvert.SerializeObject(expectedDocs),
      JsonConvert.SerializeObject(docs)
    );
  }

  [Fact]
  public async Task ReplaceOne_ItShouldReplaceTheDocumentInTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    docs[1].Name = "replaced name";
    await this._sut.ReplaceOne<TestDoc>(DB_NAME, COLL_NAME, docs[1], docs[1].Id);

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);
    var actualDocs = await coll.Find(FilterDefinition<TestDoc>.Empty).ToListAsync();

    Assert.Equal(
      JsonConvert.SerializeObject(docs),
      JsonConvert.SerializeObject(actualDocs)
    );
  }

  [Fact]
  public async Task DeleteOne_ItShouldSoftDeleteTheDocumentInTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    var startTs = DateTime.Now;
    await this._sut.DeleteOne<TestDoc>(DB_NAME, COLL_NAME, docs[1].Id);
    var endTs = DateTime.Now;

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);
    var actualDocs = await coll.Find(FilterDefinition<TestDoc>.Empty).ToListAsync();

    Assert.InRange<DateTime>((DateTime)actualDocs[1].DeletedAt, startTs, endTs);
    actualDocs[1].DeletedAt = null;
    Assert.Equal(
      JsonConvert.SerializeObject(docs),
      JsonConvert.SerializeObject(actualDocs)
    );
  }

  [Fact]
  public async Task UpdateOne_ItShouldUpdateTheDocumentInTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    await this._sut.UpdateOne<TestDoc>(
      DB_NAME, COLL_NAME, new BsonDocument { { "name", "test name" } }, new BsonDocument { { "$set", new BsonDocument { { "name", "updated name" } } } }
    );
    docs[0].Name = "updated name";

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);
    var actualDocs = await coll.Find(FilterDefinition<TestDoc>.Empty).ToListAsync();

    Assert.Equal(
      JsonConvert.SerializeObject(docs),
      JsonConvert.SerializeObject(actualDocs)
    );
  }

  [Fact]
  public async Task UpdateMany_ItShouldUpdateTheDocumentsInTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    await this._sut.UpdateMany<TestDoc>(
      DB_NAME, COLL_NAME, new BsonDocument { { "name", "test name" } }, new BsonDocument { { "$set", new BsonDocument { { "name", "updated name" } } } }
    );
    docs[0].Name = "updated name";
    docs[2].Name = "updated name";

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);
    var actualDocs = await coll.Find(FilterDefinition<TestDoc>.Empty).ToListAsync();

    Assert.Equal(
      JsonConvert.SerializeObject(docs),
      JsonConvert.SerializeObject(actualDocs)
    );
  }

  [Fact]
  public async Task Find_ItShouldReturnTheRelevantDocumentsInTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name", DeletedAt = DateTime.Now },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "yet another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "and another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    var actualDocs = await this._sut.Find<TestDoc>(
      DB_NAME, COLL_NAME, 1, 2, new BsonDocument { { "name", "another test name" } }, false
    );

    Assert.Equal(
      JsonConvert.SerializeObject(
        new FindResult<TestDoc>
        {
          Metadata = new FindResultMetadata { Page = 1, PageSize = 2, TotalCount = 5, TotalPages = 3 },
          Data = [docs[3], docs[5]]
        }
      ),
      JsonConvert.SerializeObject(actualDocs)
    );
  }

  [Fact]
  public async Task CreateOneIndex_ItShouldCreateTheIndexInTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name", DeletedAt = DateTime.Now },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "yet another test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    await this._sut.CreateOneIndex<TestDoc>(
      DB_NAME, COLL_NAME, new BsonDocument { { "name", 1 } }, new CreateIndexOptions { Unique = true, Name = "testIndex" }
    );

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);

    var indexList = coll.Indexes.List().ToList();
    var indexDoc = indexList.SingleOrDefault(index => index["name"] == "testIndex");

    Assert.NotNull(indexDoc);
    Assert.True(indexDoc["unique"].AsBoolean);
    Assert.Equal(new BsonDocument { { "name", 1 } }, indexDoc["key"].AsBsonDocument);
  }

  [Fact]
  public async Task WatchDb_ItShouldReceiveChangesFromTheCollection()
  {
    TestDoc[] docs = [
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "test name" },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "another test name", DeletedAt = DateTime.Now },
      new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "yet another test name" },
    ];
    await this._dbFixtures.InsertFixtures(
      [COLL_NAME],
      new Dictionary<string, TestDoc[]>
      {
        { COLL_NAME, docs }
      }
    );

    var db = this._client.GetDatabase(DB_NAME);
    var coll = db.GetCollection<TestDoc>(COLL_NAME);

    var cts = new CancellationTokenSource(5000);
    List<WatchData> actualChanges = new List<WatchData> { };
    var watchTask = Task.Run(async () =>
    {
      try
      {
        await foreach (WatchData change in this._sut.WatchDb(DB_NAME, null, cts.Token))
        {
          actualChanges.Add(change);
          cts.Cancel();
          break;
        }
      }
      catch (OperationCanceledException) when (cts.IsCancellationRequested) { /* normal */ }
    });

    await Task.Delay(500);

    var expectedDoc = new TestDoc { Id = ObjectId.GenerateNewId().ToString(), Name = "watchdb test name" };
    Dictionary<string, dynamic?> expectedDocDict = new Dictionary<string, dynamic?>
    {
      { "_id", expectedDoc.Id },
      { "name", expectedDoc.Name },
      { "deleted_at", null },
    };
    coll.InsertOne(expectedDoc);

    try
    {
      await Task.Delay(-1, cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    { }

    Assert.Single(actualChanges);
    Assert.Equal(
      JsonConvert.SerializeObject(new ChangeSource { DbName = DB_NAME, CollName = COLL_NAME }),
      JsonConvert.SerializeObject(actualChanges[0].Source)
    );
    Assert.Equal(
      JsonConvert.SerializeObject(new ChangeRecord { Id = expectedDoc.Id, ChangeType = ChangeRecordTypes.Insert, Document = expectedDocDict }),
      JsonConvert.SerializeObject(actualChanges[0].ChangeRecord)
    );
  }
}

public class TestDoc
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [BsonIgnoreIfDefault]
  public string? Id { get; set; }

  [JsonPropertyName("name")]
  [JsonProperty("name")]
  [BsonElement("name")]
  public required string Name { get; set; }

  [JsonPropertyName("deleted_at")]
  [JsonProperty("deleted_at")]
  [BsonElement("deleted_at")]
  public DateTime? DeletedAt { get; set; }
}