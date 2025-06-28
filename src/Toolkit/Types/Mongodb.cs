using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace Toolkit.Types;

public struct MongoDbInputs
{
  public required IMongoClient Client { get; set; }
}

public interface IMongodb
{
  public Task InsertOne<T>(string dbName, string collName, T document);
  public Task InsertMany<T>(string dbName, string collName, T[] documents);
  public Task ReplaceOne<T>(string dbName, string collName, T document,
    string id);
  public Task DeleteOne<T>(string dbName, string collName, string id);
  public Task<UpdateRes> UpdateOne<T>(
    string dbName, string collName, BsonDocument filter, BsonDocument update,
    UpdateOptions? updateOptions = null
  );
  public Task<FindResult<T>> Find<T>(string dbName, string collName, int page,
    int size, BsonDocument? match, bool showDeleted);
  public Task<string> CreateOneIndex<T>(
    string dbName, string collName, BsonDocument document,
    CreateIndexOptions? indexOpts = null
  );
  public IAsyncEnumerable<WatchData> WatchDb(string dbName,
    ResumeData? resumeData, CancellationToken cancellationToken);
}

public struct AggregateResult<T>
{
  [BsonElement("metadata")]
  public AggregateResultMetadata[] Metadata { get; set; }

  [BsonElement("data")]
  public T[] Data { get; set; }
}

public struct AggregateResultMetadata
{
  [BsonElement("totalCount")]
  public int TotalCount { get; set; }
}

public struct FindResult<T>
{
  [JsonPropertyName("metadata")]
  [JsonProperty("metadata")]
  public FindResultMetadata Metadata { get; set; }

  [JsonPropertyName("data")]
  [JsonProperty("data")]
  public T[] Data { get; set; }
}

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

public struct WatchData
{
  public required DateTime ChangeTime { get; set; }

  public required ResumeData ResumeData { get; set; }

  public required ChangeSource Source { get; set; }

  public ChangeRecord? ChangeRecord { get; set; }
}

public record ChangeRecordTypes(int Id, string Name)
{
  public static ChangeRecordTypes Insert { get; } = new(1, "insert");

  public static ChangeRecordTypes Delete { get; } = new(2, "delete");

  public static ChangeRecordTypes Updated { get; } = new(3, "update");

  public static ChangeRecordTypes Replace { get; } = new(4, "replace");

  public override string ToString() => Name;
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

public struct UpdateRes
{
  public required long DocumentsFound { get; set; }

  public required long ModifiedCount { get; set; }

  public string? UpsertedId { get; set; }
}