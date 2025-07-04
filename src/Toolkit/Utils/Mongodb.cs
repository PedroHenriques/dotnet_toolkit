using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Toolkit.Types;

namespace Toolkit.Utils;

public class Mongodb
{
  [ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the MongoDb SDK is done.")]
  public static MongoDbInputs PrepareInputs(string conStr)
  {
    return new MongoDbInputs
    {
      Client = new MongoClient(conStr),
    };
  }

  public static ChangeStreamOptions? BuildStreamOpts(ResumeData resumeData)
  {
    if (resumeData.ResumeToken != null)
    {
      ExpandoObject? token = JsonConvert.DeserializeObject<ExpandoObject>(
        resumeData.ResumeToken, new ExpandoObjectConverter());

      if (token != null)
      {
        return new ChangeStreamOptions
        {
          ResumeAfter = new BsonDocument(token),
          FullDocument = ChangeStreamFullDocumentOption.WhenAvailable,
        };
      }
    }

    if (resumeData.ClusterTime != null)
    {
      return new ChangeStreamOptions
      {
        StartAtOperationTime = new BsonTimestamp(long.Parse(
          resumeData.ClusterTime)),
        FullDocument = ChangeStreamFullDocumentOption.WhenAvailable,
      };
    }

    return new ChangeStreamOptions
    {
      FullDocument = ChangeStreamFullDocumentOption.WhenAvailable,
    };
  }

  public static ChangeRecord BuildChangeRecord(BsonDocument change)
  {
    ChangeRecord result = new ChangeRecord
    {
      ChangeType = ChangeRecordTypes.Insert,
      Id = "",
    };

    try
    {
      result.Id = change["documentKey"]["_id"].ToString();
    }
    catch
    {
      throw new Exception(
        $"The change stream's backing document with ID {change.GetValue("_id")} doesn't contain the value of 'documentKey'."
      );
    }

    switch (change.GetValue("operationType").ToString())
    {
      case "insert":
        result.Document = BuildDictFromBsonDoc(change["fullDocument"]
          .AsBsonDocument);
        break;
      case "replace":
        result.ChangeType = ChangeRecordTypes.Replace;
        result.Document = BuildDictFromBsonDoc(change["fullDocument"]
          .AsBsonDocument);
        break;
      case "delete":
        result.ChangeType = ChangeRecordTypes.Delete;
        break;
      case "update":
        BsonDocument? updatedFields = change["updateDescription"]
          ["updatedFields"].AsBsonDocument;

        if (updatedFields.Contains("deleted_at") &&
          updatedFields["deleted_at"].IsBsonNull == false)
        {
          result.ChangeType = ChangeRecordTypes.Delete;
          break;
        }
        result.ChangeType = ChangeRecordTypes.Updated;
        result.Document = BuildDictFromBsonDoc(change["fullDocument"]
          .AsBsonDocument);
        break;
      default:
        break;
    }

    return result;
  }

  private static Dictionary<string, dynamic?> BuildDictFromBsonDoc(
    BsonDocument doc)
  {
    Dictionary<string, dynamic?> dict = new Dictionary<string, dynamic?>();
    foreach (var elem in doc.Elements)
    {
      dict.Add(elem.Name, BsonTypeMapper.MapToDotNetValue(elem.Value));
    }
    return dict;
  }
}