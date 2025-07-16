using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests;

public class Entity
{
  public required string Name { get; set; }
}

[Trait("Type", "Unit")]
public class MongodbTests : IDisposable
{
  private readonly Mock<IMongoClient> _dbClientMock;
  private readonly Mock<IMongoDatabase> _dbDatabaseMock;
  private readonly Mock<IMongoCollection<Entity>> _dbCollectionMock;
  private readonly Mock<IAsyncCursor<AggregateResult<Entity>>> _aggregateCursorMock;
  private readonly Mock<IMongoIndexManager<Entity>> _indexManagerMock;
  private readonly MongoDbInputs _mongoDbInputs;

  public MongodbTests()
  {
    this._dbClientMock = new Mock<IMongoClient>(MockBehavior.Strict);
    this._dbDatabaseMock = new Mock<IMongoDatabase>(MockBehavior.Strict);
    this._dbCollectionMock = new Mock<IMongoCollection<Entity>>(MockBehavior.Strict);
    this._aggregateCursorMock = new Mock<IAsyncCursor<AggregateResult<Entity>>>();
    this._indexManagerMock = new Mock<IMongoIndexManager<Entity>>(MockBehavior.Strict);

    this._dbClientMock.Setup(s => s.GetDatabase(It.IsAny<string>(), null))
      .Returns(this._dbDatabaseMock.Object);

    this._dbDatabaseMock.Setup(s => s.GetCollection<Entity>(It.IsAny<string>(), null))
      .Returns(this._dbCollectionMock.Object);
    this._dbCollectionMock.Setup(s => s.InsertOneAsync(It.IsAny<Entity>(), null, default))
      .Returns(Task.Delay(1));
    this._dbCollectionMock.Setup(s => s.InsertManyAsync(It.IsAny<Entity[]>(), null, default))
      .Returns(Task.Delay(1));
    this._dbCollectionMock.Setup(s => s.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default))
      .Returns(Task.FromResult(new ReplaceOneResult.Acknowledged(1, 1, null) as ReplaceOneResult));
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(1, 1, null) as UpdateResult));
    this._dbCollectionMock.Setup(s => s.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(1, 1, null) as UpdateResult));
    this._dbCollectionMock.Setup(s => s.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default))
      .Returns(Task.FromResult(this._aggregateCursorMock.Object));
    this._dbCollectionMock.Setup(s => s.Indexes)
      .Returns(this._indexManagerMock.Object);

    this._indexManagerMock.Setup(s => s.CreateOneAsync(It.IsAny<CreateIndexModel<Entity>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult("test index name"));

    this._aggregateCursorMock.Setup(s => s.Current).Returns(new[] { new AggregateResult<Entity> { Metadata = new[] { new AggregateResultMetadata { } } } });
    this._aggregateCursorMock.Setup(s => s.MoveNextAsync(default)).Returns(Task.FromResult(true));

    this._mongoDbInputs = new MongoDbInputs
    {
      Client = this._dbClientMock.Object,
    };
  }

  public void Dispose()
  {
    this._dbClientMock.Reset();
    this._dbDatabaseMock.Reset();
    this._dbCollectionMock.Reset();
    this._aggregateCursorMock.Reset();
    this._indexManagerMock.Reset();
  }

  [Fact]
  public async Task InsertOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.InsertOne<Entity>("test db name", "", new Entity { Name = "" });
    this._dbClientMock.Verify(m => m.GetDatabase("test db name", null), Times.Once());
  }

  [Fact]
  public async Task InsertOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.InsertOne<Entity>("", "test col name", new Entity { Name = "" });
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("test col name", null), Times.Once());
  }

  [Fact]
  public async Task InsertOne_ItShouldCallInsertOneAsyncFromTheMongoCollectionOnceWithTheProvidedDocument()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity testDoc = new Entity { Name = "" };
    await sut.InsertOne<Entity>("", "", testDoc);
    this._dbCollectionMock.Verify(m => m.InsertOneAsync(testDoc, null, default), Times.Once());
  }

  [Fact]
  public async Task InsertMany_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity[] data = new Entity[] {
      new Entity { Name = "" },
      new Entity { Name = "" },
    };
    await sut.InsertMany<Entity>("test db name", "", data);
    this._dbClientMock.Verify(m => m.GetDatabase("test db name", null), Times.Once());
  }

  [Fact]
  public async Task InsertMany_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity[] data = new Entity[] {
      new Entity { Name = "" },
      new Entity { Name = "" },
    };
    await sut.InsertMany<Entity>("", "test col name", data);
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("test col name", null), Times.Once());
  }

  [Fact]
  public async Task InsertMany_ItShouldCallInsertManyAsyncFromTheMongoCollectionOnceWithTheProvidedDocuments()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity[] data = new Entity[] {
      new Entity { Name = "" },
      new Entity { Name = "" },
    };
    await sut.InsertMany<Entity>("", "", data);
    this._dbCollectionMock.Verify(m => m.InsertManyAsync(data, null, default), Times.Once());
  }

  [Fact]
  public async Task ReplaceOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.ReplaceOne<Entity>("some test db name", "", new Entity { Name = "" }, ObjectId.GenerateNewId().ToString());
    this._dbClientMock.Verify(m => m.GetDatabase("some test db name", null), Times.Once());
  }

  [Fact]
  public async Task ReplaceOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.ReplaceOne<Entity>("", "another test col name", new Entity { Name = "" }, ObjectId.GenerateNewId().ToString());
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("another test col name", null), Times.Once());
  }

  [Fact]
  public async Task ReplaceOne_ItShouldCallReplaceOneAsyncFromTheMongoCollectionOnceWithTheCorrectFilter()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity testDoc = new Entity { Name = "" };
    ObjectId testId = ObjectId.GenerateNewId();
    await sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString());

    this._dbCollectionMock.Verify(m => m.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default));
    Assert.Equal(
      new BsonDocument {
        {
          "_id",  testId
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Document
    );
  }

  [Fact]
  public async Task ReplaceOne_ItShouldCallReplaceOneAsyncFromTheMongoCollectionOnceWithTheProvidedDocument()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity testDoc = new Entity { Name = "" };
    ObjectId testId = ObjectId.GenerateNewId();
    await sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString());

    this._dbCollectionMock.Verify(m => m.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), testDoc, null as ReplaceOptions, default));
  }

  [Fact]
  public async Task ReplaceOne_IfNoDocumentIsFound_ItShouldThrowAKeyNotFoundException()
  {
    this._dbCollectionMock.Setup(s => s.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default))
      .Returns(Task.FromResult(new ReplaceOneResult.Acknowledged(0, 1, null) as ReplaceOneResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity testDoc = new Entity { Name = "" };
    ObjectId testId = ObjectId.GenerateNewId();

    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString()));
    Assert.Equal($"Could not find the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async Task ReplaceOne_IfNoDocumentIsReplaced_ItShouldThrowAnException()
  {
    this._dbCollectionMock.Setup(s => s.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default))
      .Returns(Task.FromResult(new ReplaceOneResult.Acknowledged(1, 0, null) as ReplaceOneResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Entity testDoc = new Entity { Name = "" };
    ObjectId testId = ObjectId.GenerateNewId();

    Exception exception = await Assert.ThrowsAsync<Exception>(() => sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString()));
    Assert.Equal($"Could not replace the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async Task DeleteOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.DeleteOne<Entity>("another test db name", "", ObjectId.GenerateNewId().ToString());
    this._dbClientMock.Verify(m => m.GetDatabase("another test db name", null), Times.Once());
  }

  [Fact]
  public async Task DeleteOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.DeleteOne<Entity>("", "random test col name", ObjectId.GenerateNewId().ToString());
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("random test col name", null), Times.Once());
  }

  [Fact]
  public async Task DeleteOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectFilter()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    ObjectId testId = ObjectId.GenerateNewId();
    await sut.DeleteOne<Entity>("", "", testId.ToString());

    this._dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      new BsonDocument {
        {
          "_id",  testId
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Document
    );
  }

  [Fact]
  public async Task DeleteOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectUpdateDefinition()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    ObjectId testId = ObjectId.GenerateNewId();
    await sut.DeleteOne<Entity>("", "", testId.ToString());

    this._dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      new BsonDocument {
        {
          "$currentDate", new BsonDocument {
            { "deleted_at", true }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[1] as dynamic).Document
    );
  }

  [Fact]
  public async Task DeleteOne_IfNoDocumentIsFound_ItShouldThrowAKeyNotFoundException()
  {
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(0, 1, null) as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    ObjectId testId = ObjectId.GenerateNewId();

    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DeleteOne<Entity>("", "", testId.ToString()));
    Assert.Equal($"Could not find the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async Task DeleteOne_IfNoDocumentIsUpdated_ItShouldThrowAnException()
  {
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(1, 0, null) as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    ObjectId testId = ObjectId.GenerateNewId();

    Exception exception = await Assert.ThrowsAsync<Exception>(() => sut.DeleteOne<Entity>("", "", testId.ToString()));
    Assert.Equal($"Could not update the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async Task UpdateOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.UpdateOne<Entity>("another test db name", "", new BsonDocument { }, new BsonDocument { });
    this._dbClientMock.Verify(m => m.GetDatabase("another test db name", null), Times.Once());
  }

  [Fact]
  public async Task UpdateOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.UpdateOne<Entity>("", "random test col name", new BsonDocument { }, new BsonDocument { });
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("random test col name", null), Times.Once());
  }

  [Fact]
  public async Task UpdateOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectFilter()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var filter = new BsonDocument { };
    await sut.UpdateOne<Entity>("", "", filter, new BsonDocument { });

    this._dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      filter,
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Document
    );
  }

  [Fact]
  public async Task UpdateOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectUpdateDefinition()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var update = new BsonDocument { };
    await sut.UpdateOne<Entity>("", "", new BsonDocument { }, update);

    this._dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      update,
      (this._dbCollectionMock.Invocations[0].Arguments[1] as dynamic).Document
    );
  }

  [Fact]
  public async Task UpdateOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectUpdateOptions()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var updateOpts = new UpdateOptions { };
    await sut.UpdateOne<Entity>("", "", new BsonDocument { }, new BsonDocument { }, updateOpts);

    this._dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      updateOpts,
      this._dbCollectionMock.Invocations[0].Arguments[2] as dynamic
    );
  }

  [Fact]
  public async Task UpdateOne_ItShouldReturnTheExpectedUpdateResInstance()
  {
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(10, 121, null) as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var res = await sut.UpdateOne<Entity>("", "", new BsonDocument { }, new BsonDocument { });

    Assert.Equal(
      new UpdateRes
      {
        DocumentsFound = 10,
        ModifiedCount = 121,
      },
      res
    );
  }

  [Fact]
  public async Task UpdateOne_IfADocumentWasUpserted_ItShouldReturnTheExpectedUpdateResInstance()
  {
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(0, 0, "some upserted id") as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var res = await sut.UpdateOne<Entity>("", "", new BsonDocument { }, new BsonDocument { });

    Assert.Equal(
      new UpdateRes
      {
        DocumentsFound = 0,
        ModifiedCount = 0,
        UpsertedId = "some upserted id",
      },
      res
    );
  }

  [Fact]
  public async Task UpdateOne_IfTheResultIsNotAcknowledged_ItShouldThrowAKeyNotFoundException()
  {
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(UpdateResult.Unacknowledged.Instance as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var filter = new BsonDocument { { "id", 1 } };
    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateOne<Entity>("", "", filter, new BsonDocument { }));
    Assert.Equal($"Could not find any documents with the provided filter: '{filter}'", exception.Message);
  }

  [Fact]
  public async Task UpdateOne_IfNoDocumentIsFound_ItShouldThrowAKeyNotFoundException()
  {
    this._dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(0, 1, null) as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var filter = new BsonDocument { { "id", 1 } };
    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateOne<Entity>("", "", filter, new BsonDocument { }));
    Assert.Equal($"Could not find any documents with the provided filter: '{filter}'", exception.Message);
  }

  [Fact]
  public async Task UpdateMany_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.UpdateMany<Entity>("another test db name", "", new BsonDocument { }, new BsonDocument { });
    this._dbClientMock.Verify(m => m.GetDatabase("another test db name", null), Times.Once());
  }

  [Fact]
  public async Task UpdateMany_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.UpdateMany<Entity>("", "random test col name", new BsonDocument { }, new BsonDocument { });
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("random test col name", null), Times.Once());
  }

  [Fact]
  public async Task UpdateMany_ItShouldCallUpdateManyAsyncFromTheMongoCollectionOnceWithTheCorrectFilter()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var filter = new BsonDocument { };
    await sut.UpdateMany<Entity>("", "", filter, new BsonDocument { });

    this._dbCollectionMock.Verify(m => m.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      filter,
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Document
    );
  }

  [Fact]
  public async Task UpdateMany_ItShouldCallUpdateManyAsyncFromTheMongoCollectionOnceWithTheCorrectUpdateDefinition()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var update = new BsonDocument { };
    await sut.UpdateMany<Entity>("", "", new BsonDocument { }, update);

    this._dbCollectionMock.Verify(m => m.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      update,
      (this._dbCollectionMock.Invocations[0].Arguments[1] as dynamic).Document
    );
  }

  [Fact]
  public async Task UpdateMany_ItShouldCallUpdateManyAsyncFromTheMongoCollectionOnceWithTheCorrectUpdateOptions()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var updateOpts = new UpdateOptions { };
    await sut.UpdateMany<Entity>("", "", new BsonDocument { }, new BsonDocument { }, updateOpts);

    this._dbCollectionMock.Verify(m => m.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()));
    Assert.Equal(
      updateOpts,
      this._dbCollectionMock.Invocations[0].Arguments[2] as dynamic
    );
  }

  [Fact]
  public async Task UpdateMany_ItShouldReturnTheExpectedUpdateResInstance()
  {
    this._dbCollectionMock.Setup(s => s.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(10, 121, null) as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var res = await sut.UpdateMany<Entity>("", "", new BsonDocument { }, new BsonDocument { });

    Assert.Equal(
      new UpdateRes
      {
        DocumentsFound = 10,
        ModifiedCount = 121,
      },
      res
    );
  }

  [Fact]
  public async Task UpdateMany_IfADocumentWasUpserted_ItShouldReturnTheExpectedUpdateResInstance()
  {
    this._dbCollectionMock.Setup(s => s.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(0, 0, "some upserted id") as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var res = await sut.UpdateMany<Entity>("", "", new BsonDocument { }, new BsonDocument { });

    Assert.Equal(
      new UpdateRes
      {
        DocumentsFound = 0,
        ModifiedCount = 0,
        UpsertedId = "some upserted id",
      },
      res
    );
  }

  [Fact]
  public async Task UpdateMany_IfTheResultIsNotAcknowledged_ItShouldThrowAKeyNotFoundException()
  {
    this._dbCollectionMock.Setup(s => s.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(UpdateResult.Unacknowledged.Instance as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var filter = new BsonDocument { { "id", 1 } };
    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateMany<Entity>("", "", filter, new BsonDocument { }));
    Assert.Equal($"Could not find any documents with the provided filter: '{filter}'", exception.Message);
  }

  [Fact]
  public async Task UpdateMany_IfNoDocumentIsFound_ItShouldThrowAKeyNotFoundException()
  {
    this._dbCollectionMock.Setup(s => s.UpdateManyAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), It.IsAny<UpdateOptions?>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(0, 1, null) as UpdateResult));

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    var filter = new BsonDocument { { "id", 1 } };
    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateMany<Entity>("", "", filter, new BsonDocument { }));
    Assert.Equal($"Could not find any documents with the provided filter: '{filter}'", exception.Message);
  }

  [Fact]
  public async Task Find_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("find test db name", "", 0, 0, null, false);
    this._dbClientMock.Verify(m => m.GetDatabase("find test db name", null), Times.Once());
  }

  [Fact]
  public async Task Find_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "random find test col name", 0, 0, null, false);
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("random find test col name", null), Times.Once());
  }

  [Fact]
  public async Task Find_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedFirstStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 0, 0, null, false);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$match", new BsonDocument { { "deleted_at", BsonNull.Value } }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[0]
    );
  }

  [Fact]
  public async Task Find_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedSecondStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 0, 0, null, false);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$sort", new BsonDocument
          {
            { "_id", 1 }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[1]
    );
  }

  [Fact]
  public async Task Find_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedThirdStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 3, 10, null, false);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$facet", new BsonDocument {
            { "metadata", new BsonArray {
              new BsonDocument { { "$count", "totalCount" } }
            } },
            { "data", new BsonArray {
              new BsonDocument { { "$skip", 20 } },
              new BsonDocument { { "$limit", 10 } }
            } }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[2]
    );
  }

  [Fact]
  public async Task Find_ItShouldReturnTheExpectedValue()
  {
    AggregateResult<Entity> aggregateRes = new AggregateResult<Entity>
    {
      Metadata = new[] {
        new AggregateResultMetadata {
          TotalCount = 123
        }
      },
      Data = new[] {
        new Entity { Name = "" },
        new Entity { Name = "" },
        new Entity { Name = "" },
        new Entity { Name = "" },
      }
    };
    this._aggregateCursorMock.Setup(s => s.Current).Returns(new[] { aggregateRes });

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Assert.Equal(
      new FindResult<Entity>
      {
        Metadata = new FindResultMetadata
        {
          Page = 6,
          PageSize = 2,
          TotalCount = 123,
          TotalPages = 62
        },
        Data = aggregateRes.Data
      },
      await sut.Find<Entity>("", "", 6, 2, null, false)
    );
  }

  [Fact]
  public async Task Find_IfTheCursorIsEmpty_ItShouldReturnTheExpectedValue()
  {
    AggregateResult<Entity> aggregateRes = new AggregateResult<Entity>
    {
      Metadata = Array.Empty<AggregateResultMetadata>(),
      Data = new[] {
        new Entity { Name = "" },
      }
    };
    this._aggregateCursorMock.Setup(s => s.Current).Returns(new[] { aggregateRes });

    IMongodb sut = new Mongodb(this._mongoDbInputs);

    Assert.Equal(
      new FindResult<Entity>
      {
        Metadata = new FindResultMetadata
        {
          Page = 16,
          PageSize = 20,
          TotalCount = 0,
          TotalPages = 0
        },
        Data = aggregateRes.Data
      },
      await sut.Find<Entity>("", "", 16, 20, null, false)
    );
  }

  [Fact]
  public async Task Find_IfAMatchBsondocumentIsProvided_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedFirstStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);
    BsonDocument testMatch = new BsonDocument
    {
      { "some property", "hello from test" }
    };

    await sut.Find<Entity>("", "", 0, 0, testMatch, false);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument {
        {
          "$match", new BsonDocument {
            {
              "$and",
              new BsonArray {
                testMatch,
                new BsonDocument { { "deleted_at", BsonNull.Value } },
              }
            }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[0]
    );
  }

  [Fact]
  public async Task Find_IfAMatchBsondocumentIsProvided_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedSecondStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 0, 0, new BsonDocument { { "$match", "" } }, false);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$sort", new BsonDocument
          {
            { "_id", 1 }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[1]
    );
  }

  [Fact]
  public async Task Find_IfAMatchBsondocumentIsProvided_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedThirdStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 3, 10, new BsonDocument { { "$match", "" } }, false);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$facet", new BsonDocument {
            { "metadata", new BsonArray {
              new BsonDocument { { "$count", "totalCount" } }
            } },
            { "data", new BsonArray {
              new BsonDocument { { "$skip", 20 } },
              new BsonDocument { { "$limit", 10 } }
            } }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[2]
    );
  }

  [Fact]
  public async Task Find_IfShowDeletedIsTrue_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedFirstStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 0, 0, null, true);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$sort", new BsonDocument
          {
            { "_id", 1 }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[0]
    );
  }

  [Fact]
  public async Task Find_IfShowDeletedIsTrue_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedSecondStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.Find<Entity>("", "", 0, 0, null, true);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument
      {
        {
          "$facet", new BsonDocument {
            { "metadata", new BsonArray {
              new BsonDocument { { "$count", "totalCount" } }
            } },
            { "data", new BsonArray {
              new BsonDocument { { "$skip", 0 } },
              new BsonDocument { { "$limit", 0 } }
            } }
          }
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[1]
    );
  }

  [Fact]
  public async Task Find_IfAMatchBsondocumentIsProvidedAndShowDeletedIsTrue_ItShouldCallAggregateAsyncFromTheMongoCollectionOnceWithTheExpectedFirstStageOfThePipeline()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);
    BsonDocument testMatch = new BsonDocument
    {
      { "some property", "hello from test" }
    };

    await sut.Find<Entity>("", "", 0, 0, testMatch, true);
    this._dbCollectionMock.Verify(m => m.AggregateAsync(It.IsAny<PipelineDefinition<Entity, AggregateResult<Entity>>>(), null, default), Times.Once);
    Assert.Equal(
      new BsonDocument {
        {
          "$match", testMatch
        }
      },
      (this._dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Documents[0]
    );
  }

  [Fact]
  public async Task CreateOneIndex_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.CreateOneIndex<Entity>("find test db name", "", new BsonDocument { });
    this._dbClientMock.Verify(m => m.GetDatabase("find test db name", null), Times.Once());
  }

  [Fact]
  public async Task CreateOneIndex_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.CreateOneIndex<Entity>("", "random find test col name", new BsonDocument { });
    this._dbDatabaseMock.Verify(m => m.GetCollection<Entity>("random find test col name", null), Times.Once());
  }

  [Fact]
  public async Task CreateOneIndex_ItShouldCallCreateOneAsyncFromTheIndexManagerOfTheMongoCollectionOnce()
  {
    IMongodb sut = new Mongodb(this._mongoDbInputs);

    await sut.CreateOneIndex<Entity>("", "", new BsonDocument { }, new CreateIndexOptions { });
    this._indexManagerMock.Verify(m => m.CreateOneAsync(It.IsAny<CreateIndexModel<Entity>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()), Times.Once());
  }
}