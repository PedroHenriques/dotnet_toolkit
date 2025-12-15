using Toolkit.Types;
using MongoDB.Bson;
using MongoDB.Driver;
using DbUtils = Toolkit.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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

    BsonDocument sortContent = sort ?? new BsonDocument { { "_id", 1 } };

    stages.Add(new BsonDocument {
      { "$sort", sortContent }
    });

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

  public async Task<CounterResult> Counter(
    string dbName, string collName, int page, int size, string valueFieldPath,
    string? distinctDocField = null, bool showDeleted = false, bool uniqueWithinDoc = true,
    BsonDocument? match = null, BsonDocument? sort = null
  )
  {
    var db = this._inputs.Client.GetDatabase(dbName);
    var coll = db.GetCollection<BsonDocument>(collName);

    var stages = new List<BsonDocument>();

    var activeFilter = new BsonDocument(this._inputs.DeletedAtPropName, BsonNull.Value);
    BsonDocument? matchContent = null;
    if (match != null && !showDeleted)
    {
      matchContent = new BsonDocument("$and", new BsonArray { match, activeFilter });
    }
    else if (match != null)
    {
      matchContent = match;
    }
    else if (!showDeleted)
    {
      matchContent = activeFilter;
    }

    if (matchContent != null)
    {
      stages.Add(new BsonDocument("$match", matchContent));
    }

    BsonDocument idProjection = new BsonDocument { };
    if (string.IsNullOrEmpty(distinctDocField))
    {
      distinctDocField = "_id";
      idProjection = new BsonDocument { { "_id", 0 } };
    }
    else
    {
      idProjection = new BsonDocument { { distinctDocField, "$_id" } };
    }

    if (sort != null)
    {
      stages.Add(new BsonDocument("$sort", sort));
    }

    stages.Add(new BsonDocument(
      "$group",
      new BsonDocument {
        { "_id", $"${distinctDocField}" },
        { "doc", new BsonDocument("$first", "$$ROOT") }
      }
    ));

    var docValuePath = $"$doc.{valueFieldPath}";
    var normalizedArrayExpr = new BsonDocument(
      "$cond",
      new BsonArray {
        new BsonDocument("$isArray", docValuePath),
        docValuePath,
        new BsonDocument(
          "$cond",
          new BsonArray {
            new BsonDocument("$eq", new BsonArray { docValuePath, BsonNull.Value }),
            new BsonArray(),
            new BsonArray { docValuePath }
          }
        )
      }
    );

    BsonValue countExpr = new BsonDocument(
      "$size",
      new BsonDocument("$setUnion", new BsonArray { normalizedArrayExpr, new BsonArray() })
    );
    if (uniqueWithinDoc == false)
    {
      countExpr = new BsonDocument("$size", normalizedArrayExpr);
    }

    stages.Add(new BsonDocument(
      "$project",
      new BsonDocument
      {
        { "keyField", new BsonDocument("$toString", "$_id") },
        idProjection.Elements.FirstOrDefault(),
        { "count", countExpr }
      }
    ));

    var skip = Math.Max(0, (page - 1) * size);
    stages.Add(new BsonDocument(
      "$facet",
      new BsonDocument
      {
        { "data", new BsonArray
          {
            new BsonDocument("$skip", skip),
            new BsonDocument("$limit", size)
          }
        },
        {
          "overall", new BsonArray
          {
            new BsonDocument("$group", new BsonDocument
            {
              { "_id", BsonNull.Value },
              { "sumOfCounts", new BsonDocument("$sum", "$count") },
              { "totalCount",  new BsonDocument("$sum", 1) }
            })
          }
        }
      }
    ));

    var cursor = await coll.AggregateAsync<BsonDocument>(stages);
    var facet = await cursor.FirstOrDefaultAsync();
    if (facet == null)
    {
      facet = new BsonDocument
      {
        { "overall", new BsonArray() },
        { "data", new BsonArray() }
      };
    }

    var overallArr = facet.GetValue("overall", new BsonArray()).AsBsonArray;
    int sumOfCounts = 0;
    if (overallArr.Count > 0 && overallArr[0].AsBsonDocument.Contains("sumOfCounts"))
    {
      sumOfCounts = overallArr[0]["sumOfCounts"].ToInt32();
    }

    int totalCountAllRows = 0;
    if (overallArr.Count > 0 && overallArr[0].AsBsonDocument.Contains("totalCount"))
    {
      totalCountAllRows = overallArr[0]["totalCount"].ToInt32();
    }

    var dataArr = facet.GetValue("data", new BsonArray()).AsBsonArray;
    CounterResultData[] data = dataArr
      .Select(elem =>
      {
        var elemDoc = elem.AsBsonDocument;

        string keyField = "";
        if (elemDoc.GetValue("keyField", BsonNull.Value).IsBsonNull == false)
        {
          keyField = elemDoc["keyField"].AsString;
        }

        return new CounterResultData
        {
          KeyField = keyField,
          Count = elemDoc.GetValue("count", 0).ToInt32(),
        };
      })
      .ToArray();

    return new CounterResult
    {
      Metadata = new CounterResultMetadata
      {
        SumOfCounts = sumOfCounts,
        TotalCount = totalCountAllRows,
        Page = page,
        PageSize = size,
        TotalPages = (int)Math.Ceiling(totalCountAllRows / (double)Math.Max(1, size))
      },
      Data = data
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
  public async IAsyncEnumerable<WatchData> WatchDb(
    string dbName, ResumeData? resumeData = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default,
    int batchSize = 100
  )
  {
    var attempt = 0;

    while (cancellationToken.IsCancellationRequested == false)
    {
      var startKind = attempt == 0 ? WatchKind.Started : WatchKind.Resumed;
      yield return new WatchData
      {
        Kind = startKind,
        ChangeTime = DateTime.Now,
        Source = new ChangeSource
        {
          DbName = dbName,
          CollName = "",
        },
      };

      IAsyncEnumerable<WatchData> stream = StreamHandler(
        dbName, resumeData, cancellationToken, batchSize
      );

      await foreach (var item in stream.WithCancellation(cancellationToken))
      {
        resumeData = item.ResumeData ?? resumeData;
        yield return item;
      }

      yield return new WatchData
      {
        Kind = WatchKind.Stopped,
        ChangeTime = DateTime.Now,
        Source = new ChangeSource
        {
          DbName = dbName,
          CollName = "",
        },
      };

      attempt++;
      await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
    }
  }

  [ExcludeFromCodeCoverage(Justification = "Not unit testable due to WatchAsync() being an extension method of the MongoDb SDK.")]
  private async IAsyncEnumerable<WatchData> StreamHandler(
    string dbName, ResumeData? resumeData,
    [EnumeratorCancellation] CancellationToken cancellationToken,
    int batchSize
  )
  {
    IMongoDatabase db = this._inputs.Client.GetDatabase(dbName);

    ChangeStreamOptions opts = DbUtils.Mongodb.BuildStreamOpts(
      resumeData.GetValueOrDefault(), batchSize
    );

    var cursor = await db.WatchAsync(opts, cancellationToken);

    var lastHeartbeatUtc = DateTime.UtcNow;

    while (await cursor.MoveNextAsync(cancellationToken))
    {
      var batch = cursor.Current;
      if (batch.Any() == false)
      {
        lastHeartbeatUtc = DateTime.UtcNow;
        yield return new WatchData
        {
          Kind = WatchKind.Heartbeat,
          ChangeTime = DateTime.Now,
          Source = new ChangeSource
          {
            DbName = db.DatabaseNamespace.DatabaseName,
            CollName = "",
          },
          Health = new StreamHealth { LastHeartbeatUtc = lastHeartbeatUtc },
        };
        continue;
      }

      foreach (var change in batch)
      {
        cancellationToken.ThrowIfCancellationRequested();

        ChangeRecord? changeRecord = null;
        Exception? buildChangeEx = null;
        try
        {
          changeRecord = DbUtils.Mongodb.BuildChangeRecord(
            change.BackingDocument, this._inputs.DeletedAtPropName
          );
        }
        catch (Exception ex)
        {
          buildChangeEx = ex;
        }

        if (buildChangeEx != null)
        {
          yield return new WatchData
          {
            Kind = WatchKind.Error,
            ChangeTime = DateTime.Now,
            Exception = buildChangeEx,
            Source = new ChangeSource
            {
              DbName = change.DatabaseNamespace.DatabaseName,
              CollName = change.CollectionNamespace.CollectionName,
            },
          };
          continue;
        }

        yield return new WatchData
        {
          Kind = WatchKind.Data,
          ChangeTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(change.ClusterTime.Timestamp),
          ChangeRecord = changeRecord,
          ResumeData = new ResumeData
          {
            ResumeToken = change.ResumeToken.ToJson(),
            ClusterTime = change.ClusterTime?.ToString(),
          },
          Source = new ChangeSource
          {
            DbName = change.DatabaseNamespace.DatabaseName,
            CollName = change.CollectionNamespace.CollectionName,
          },
        };
      }
    }

    cursor.Dispose();
  }
}