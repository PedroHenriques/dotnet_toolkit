# Toolkit for MongoDb

## Package
This service is included in the `PJHToolkit` package.

## How to use
```c#
using Toolkit;
using Toolkit.Types;
using MongodbUtils = Toolkit.Utils.Mongodb;

MongoDbInputs mongodbInputs = MongodbUtils.PrepareInputs("your connection string");
IMongodb mongoDb = new Mongodb(mongodbInputs);
```

In the above snippet we:
- Start by using the `MongodbUtils.PrepareInputs()` utility function to let the Toolkit handle all the necessary setup for interactions with MongoDb.
- Then we instantiate the Toolkit's MongoDb class

The instance of `IMongodb` exposes the following functionality:

```c#
public interface IMongodb
{
  public Task InsertOne<T>(string dbName, string collName, T document);
  
  public Task InsertMany<T>(string dbName, string collName, T[] documents);
  
  public Task ReplaceOne<T>(string dbName, string collName, T document, string id);
  
  public Task DeleteOne<T>(string dbName, string collName, string id);

  public Task<UpdateRes> UpdateOne<T>(
    string dbName, string collName, BsonDocument filter, BsonDocument update,
    UpdateOptions? updateOptions = null
  );

  public Task<UpdateRes> UpdateMany<T>(
    string dbName, string collName, BsonDocument filter, BsonDocument update,
    UpdateOptions? updateOptions = null
  );
  
  public Task<FindResult<T>> Find<T>(string dbName, string collName, int page, int size, BsonDocument? match, bool showDeleted);
  
  public Task<string> CreateOneIndex<T>(string dbName, string collName, BsonDocument document, CreateIndexOptions? indexOpts = null);
  
  public IAsyncEnumerable<WatchData> WatchDb(string dbName, ResumeData? resumeData);
}
```

### InsertOne
Inserts the provided `document` document in the `collname` collection from the `dbName` database asynchronously.<br>
The ID of the inserted document will be added to the document.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.

**Example use**
```c#
// Some data structure
Entity myEntity = new Entity {
  Name = "test name",
  Hello = "world",
};

await mongoDb.InsertOne<Entity>("some database", "some collection", myEntity);
```

### InsertMany
Inserts the provided `documents` documents in the `collname` collection from the `dbName` database asynchronously.<br>
The ID of each inserted document will be added to the respective document.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.

**Example use**
```c#
// Array of some data structure
Entity[] myEntities = [
  new Entity {
    Name = "test name",
    Hello = "world",
  },
  new Entity {
    Name = "another test name",
    Hello = "world again",
  },
];

await mongoDb.InsertMany<Entity>("some database", "some collection", myEntities);
```

### ReplaceOne
Replaces the document with the provided `id` with the provided `document` document, in the `collname` collection from the `dbName` database asynchronously.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.

**Example use**
```c#
// Some data structure
Entity myEntity = new Entity {
  Name = "test name",
  Hello = "world",
};

await mongoDb.ReplaceOne<Entity>("some database", "some collection", myEntity, "the document ID to replace");
```

### DeleteOne
Soft deletes the document with the provided `id` in the `collname` collection from the `dbName` database asynchronously.<br>
Adds the property `deleted_at` to the document, with the current timestamp, signaling that the document is no longer active.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.

**Example use**
```c#
// Entity is some data structure
await mongoDb.DeleteOne<Entity>("some database", "some collection", "the document ID to replace");
```

### UpdateOne
Updates the first document that matches the provided `filter` with the provided `update` document with the provided `updateOptions` options, in the `collname` collection from the `dbName` database asynchronously.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.<br><br>
The return type `UpdateRes` has the following schema:
```c#
public struct UpdateRes
{
  public required long DocumentsFound { get; set; }

  public required long ModifiedCount { get; set; }

  public string? UpsertedId { get; set; }
}
```

**Example use**
```c#
// Entity is some data structure
await mongoDb.UpdateOne<Entity>(
  "some database", "some collection",
  new BsonDocument {
    {
      "_id",  "some doc id"
    }
  },
  new BsonDocument {
    {
      "$set", new BsonDocument {
        { "name", "some new name" }
      }
    }
  },
  new UpdateOptions {
    IsUpsert = true
  }
);
```

### UpdateMany
Updates all the documents that matches the provided `filter` with the provided `update` document with the provided `updateOptions` options, in the `collname` collection from the `dbName` database asynchronously.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.<br><br>
The return type `UpdateRes` has the following schema:
```c#
public struct UpdateRes
{
  public required long DocumentsFound { get; set; }

  public required long ModifiedCount { get; set; }

  public string? UpsertedId { get; set; }
}
```

**Example use**
```c#
// Entity is some data structure
await mongoDb.UpdateMany<Entity>(
  "some database", "some collection",
  new BsonDocument {
    {
      "_id",  "some doc id"
    }
  },
  new BsonDocument {
    {
      "$set", new BsonDocument {
        { "name", "some new name" }
      }
    }
  },
  new UpdateOptions {
    IsUpsert = true
  }
);
```

### Find
Queries the `dbName` database and `collName` collection for documents matching a filter and in the provided `page` with the provided `size` asynchronously.<br><br>
If the `match` argument is not provided, then the query will match all documents in the targetted database and collection.<br><br>
If the `showDeleted` argument is set to `true`, then the result set will include documents that are soft deleted.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.<br><br>
The return type `FindResult` has the following schema:
```c#
public struct FindResult<T>
{
  [JsonPropertyName("metadata")]
  [JsonProperty("metadata")]
  public FindResultMetadata Metadata { get; set; }

  [JsonPropertyName("data")]
  [JsonProperty("data")]
  public T[] Data { get; set; }
}
```
with
```c#
public struct FindResultMetadata
{
  [JsonPropertyName("totalCount")]
  [JsonProperty("totalCount")]
  public int TotalCount { get; set; }

  [JsonPropertyName("page")]
  [JsonProperty("page")]
  public int Page { get; set; }

  [JsonPropertyName("pageSize")]
  [JsonProperty("pageSize")]
  public int PageSize { get; set; }

  [JsonPropertyName("totalPages")]
  [JsonProperty("totalPages")]
  public int TotalPages { get; set; }
}
```

**Example use**
```c#
BsonDocument match = new BsonDocument {
  { "some property", "should equal this string" },
};

// Entity is some data structure
await mongoDb.Find<Entity>("some database", "some collection", 2, 30, match, false);
```

### CreateOneIndex
Creates an index on the provided `dbName` database and `collName` collection for the provided `document` index specification and with the provided `indexOpts` options (if provided) asynchronously.<br>
Returns the name of the created index.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.

**Example use**
```c#
// States that the documents should be sorted ascending by the property "some property"
BsonDocument indexSpec = new BsonDocument {
  { "some property", 1 },
};

// States that the index should have this name and should not allow documents to be inserted if another document already exists matching the indexSpec above
CreateIndexOptions indexOpts = new CreateIndexOptions {
  Name = "test index name",
  Unique = true
};

// Entity is some data structure
await mongoDb.CreateOneIndex<Entity>("some database", "some collection", indexSpec, indexOpts);
```

### WatchDb
Creates a connection to a Mongo Stream that listens to changes in the provided `dbName` database.<br>
Every time the documents in that database change, either due to inserts, updates, replaces or deletes this function will `yield` an instance of the type `WatchData`, with the change event emitted by MongoDb.<br><br>
Throws Exceptions (generic and MongoDb specific) on error.

The `WatchData` type has the following schema:
```c#
public struct WatchData
{
  public required DateTime ChangeTime { get; set; }

  public required ResumeData ResumeData { get; set; }

  public required ChangeSource Source { get; set; }

  public ChangeRecord? ChangeRecord { get; set; }
}
```
with
```c#
public struct ResumeData
{
  [JsonPropertyName("resumeToken")]
  [JsonProperty("resumeToken")]
  public string? ResumeToken { get; set; }

  [JsonPropertyName("clusterTime")]
  [JsonProperty("clusterTime")]
  public string? ClusterTime { get; set; }
}

public struct ChangeSource
{
  [JsonPropertyName("dbName")]
  [JsonProperty("dbName")]
  public string DbName { get; set; }

  [JsonPropertyName("collName")]
  [JsonProperty("collName")]
  public string CollName { get; set; }
}

public struct ChangeRecord
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }

  [JsonPropertyName("changeType")]
  [JsonProperty("changeType")]
  public required ChangeRecordTypes ChangeType { get; set; }

  [JsonPropertyName("document")]
  [JsonProperty("document")]
  public Dictionary<string, dynamic?>? Document { get; set; }
}
```

**Example use**
```c#
await foreach (WatchData change in mongoDb.WatchDb("some database"))
{
  if (change.ChangeRecord != null)
  {
    // Example code of doing something with the change event
    await queue.Enqueue("some queue name", new[] {
      // ChangeQueueItem is a custom data structure of your application
      JsonConvert.SerializeObject(new ChangeQueueItem{
        ChangeTime = change.ChangeTime,
        ChangeRecord = JsonConvert.SerializeObject(change.ChangeRecord),
        Source = JsonConvert.SerializeObject(change.Source),
      }),
    });
  }
}
```