using Toolkit.Types;
using MongoDB.Bson;
using MongoDB.Driver;
using DbUtils = Toolkit.Utils;
using System.Diagnostics.CodeAnalysis;

namespace Toolkit;

public class Mongodb : IMongodb
{
  private readonly MongoDbInputs _inputs;

  public Mongodb(MongoDbInputs inputs)
  {
    this._inputs = inputs;
  }

  public Task InsertOne<T>(string dbName, string collName, T document)
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    return dbColl.InsertOneAsync(document);
  }

  public Task InsertMany<T>(string dbName, string collName, T[] documents)
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    return dbColl.InsertManyAsync(documents);
  }

  public async Task ReplaceOne<T>(string dbName, string collName, T document,
    string id)
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    ReplaceOneResult replaceRes = await dbColl.ReplaceOneAsync(
      new BsonDocument {
        {
          "_id",  ObjectId.Parse(id)
        }
      },
      document
    );

    if (replaceRes.MatchedCount == 0)
    {
      throw new KeyNotFoundException($"Could not find the document with ID '{id}'");
    }

    if (replaceRes.ModifiedCount == 0)
    {
      throw new Exception($"Could not replace the document with ID '{id}'");
    }
  }

  public async Task DeleteOne<T>(string dbName, string collName, string id)
  {
    try
    {
      var res = await UpdateOne<T>(
        dbName, collName,
        new BsonDocument {
          {
            "_id",  ObjectId.Parse(id)
          }
        },
        new BsonDocument {
          {
            "$currentDate", new BsonDocument {
              { this._inputs.DeletedAtPropName, true }
            }
          }
        }
      );

      if (res.ModifiedCount == 0)
      {
        throw new Exception($"Could not update the document with ID '{id}'");
      }
    }
    catch (KeyNotFoundException)
    {
      throw new KeyNotFoundException($"Could not find the document with ID '{id}'");
    }
  }

  public async Task<UpdateRes> UpdateOne<T>(
    string dbName, string collName, BsonDocument filter, BsonDocument update,
    UpdateOptions? updateOptions = null
  )
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    UpdateResult res = await dbColl.UpdateOneAsync(
      filter, update, updateOptions
    );

    if (res.IsAcknowledged == false || (res.MatchedCount == 0 && res.UpsertedId == null))
    {
      throw new KeyNotFoundException($"Could not find any documents with the provided filter: '{filter}'");
    }

    var returnValue = new UpdateRes
    {
      DocumentsFound = res.MatchedCount,
      ModifiedCount = res.ModifiedCount,
    };

    if (res.UpsertedId != null && res.UpsertedId.IsBsonNull == false)
    {
      returnValue.UpsertedId = res.UpsertedId.ToString();
    }

    return returnValue;
  }

  public async Task<UpdateRes> UpdateMany<T>(
    string dbName, string collName, BsonDocument filter, BsonDocument update,
    UpdateOptions? updateOptions = null
  )
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    UpdateResult res = await dbColl.UpdateManyAsync(
      filter, update, updateOptions
    );

    if (res.IsAcknowledged == false || (res.MatchedCount == 0 && res.UpsertedId == null))
    {
      throw new KeyNotFoundException($"Could not find any documents with the provided filter: '{filter}'");
    }

    var returnValue = new UpdateRes
    {
      DocumentsFound = res.MatchedCount,
      ModifiedCount = res.ModifiedCount,
    };

    if (res.UpsertedId != null && res.UpsertedId.IsBsonNull == false)
    {
      returnValue.UpsertedId = res.UpsertedId.ToString();
    }

    return returnValue;
  }

  public async Task<FindResult<T>> Find<T>(string dbName, string collName,
    int page, int size, BsonDocument? match = null, bool showDeleted = false,
    BsonDocument? sort = null, string? distinctDocField = null)
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    List<BsonDocument> stages = new List<BsonDocument>();
    BsonDocument showActiveMatch = new BsonDocument {
      { this._inputs.DeletedAtPropName, BsonNull.Value }
    };

    BsonDocument? matchContent = null;
    if (match != null && showDeleted == false)
    {
      matchContent = new BsonDocument {
        {
          "$and",
          new BsonArray { match, showActiveMatch }
        }
      };
    }
    else if (match != null)
    {
      matchContent = match;
    }
    else if (showDeleted == false)
    {
      matchContent = showActiveMatch;
    }

    if (matchContent != null)
    {
      stages.Add(new BsonDocument {
        { "$match", matchContent }
      });
    }

    if (string.IsNullOrEmpty(distinctDocField) == false)
    {
      stages.Add(new BsonDocument
      {
        { "$group", new BsonDocument
          {
            { "_id", $"${distinctDocField}" },
            { "doc", new BsonDocument("$first", "$$ROOT") }
          }
        }
      });
      stages.Add(new BsonDocument
      {
        { "$replaceRoot", new BsonDocument("newRoot", "$doc") }
      });
    }

    BsonDocument sortContent = new BsonDocument { { "_id", 1 } };
    if (sort != null)
    {
      sortContent = sort;
    }

    stages.Add(new BsonDocument {
      { "$sort", sortContent }
    });

    stages.Add(new BsonDocument
    {
      {
        "$facet", new BsonDocument {
          { "metadata", new BsonArray {
            new BsonDocument { { "$count", "totalCount" } }
          } },
          { "data", new BsonArray {
            new BsonDocument { { "$skip", (page - 1) * size } },
            new BsonDocument { { "$limit", size } }
          } }
        }
      }
    });

    IAsyncCursor<AggregateResult<T>> resultCursor = await dbColl.AggregateAsync(
      PipelineDefinition<T, AggregateResult<T>>.Create(stages)
    );

    AggregateResult<T> results = await resultCursor.FirstAsync();
    int totalCount = results.Metadata.Length == 0 ? 0 : results.Metadata.First()
      .TotalCount;

    return new FindResult<T>
    {
      Metadata = new FindResultMetadata
      {
        Page = page,
        PageSize = size,
        TotalCount = totalCount,
        TotalPages = (int)Math.Ceiling((double)totalCount / size)
      },
      Data = results.Data
    };
  }

  public Task<string> CreateOneIndex<T>(
    string dbName, string collName, BsonDocument document,
    CreateIndexOptions? indexOpts = null
  )
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    var indexKeysDef = new BsonDocumentIndexKeysDefinition<T>(document);

    return dbColl.Indexes.CreateOneAsync(
      new CreateIndexModel<T>(indexKeysDef, indexOpts)
    );
  }

  [ExcludeFromCodeCoverage(Justification = "Not unit testable due to WatchAsync() being an extension method of the MongoDb SDK.")]
  public async IAsyncEnumerable<WatchData> WatchDb(string dbName,
    ResumeData? resumeData = null, CancellationToken cancellationToken = default)
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);

    ChangeStreamOptions? opts = null;
    if (resumeData != null)
    {
      opts = DbUtils.Mongodb.BuildStreamOpts(resumeData.GetValueOrDefault());
    }

    var cursor = await db.WatchAsync(opts);

    foreach (var change in cursor.ToEnumerable())
    {
      if (cancellationToken.IsCancellationRequested)
      {
        break;
      }

      ChangeRecord? changeRecord = null;
      try
      {
        changeRecord = DbUtils.Mongodb.BuildChangeRecord(change.BackingDocument);
      }
      catch (System.Exception)
      {
        // log it
      }

      yield return new WatchData
      {
        ChangeTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
          .AddSeconds(change.ClusterTime.Timestamp),
        ChangeRecord = changeRecord,
        ResumeData = new ResumeData
        {
          ResumeToken = change.ResumeToken.ToJson(),
          ClusterTime = change.ClusterTime.ToString(),
        },
        Source = new ChangeSource
        {
          DbName = change.DatabaseNamespace.DatabaseName,
          CollName = change.CollectionNamespace.CollectionName,
        },
      };
    }

    cursor.Dispose();
  }
}