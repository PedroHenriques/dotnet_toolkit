using Moq;
using Toolkit.Types;
using StackExchange.Redis;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class RedisTests : IDisposable
{
  private readonly Mock<IConnectionMultiplexer> _redisClient;
  private readonly Mock<IDatabase> _redisDb;
  private readonly RedisInputs _inputs;

  public RedisTests()
  {
    this._redisClient = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
    this._redisDb = new Mock<IDatabase>(MockBehavior.Strict);

    this._redisClient.Setup(s => s.GetDatabase(It.IsAny<int>(), null))
      .Returns(this._redisDb.Object);

    this._redisDb.Setup(s => s.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue { }));
    this._redisDb.Setup(s => s.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<HashEntry[]>([]));
    this._redisDb.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));
    this._redisDb.Setup(s => s.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));
    this._redisDb.Setup(s => s.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.CompletedTask);
    this._redisDb.Setup(s => s.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));
    this._redisDb.Setup(s => s.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));
    this._redisDb.Setup(s => s.StreamDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), CommandFlags.None))
      .Returns(Task.FromResult((long)1));

    this._redisDb.Setup(s => s.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue { }));
    this._redisDb.Setup(s => s.StreamCreateConsumerGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));
    this._redisDb.Setup(s => s.StreamReadGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] { new StreamEntry { } }));
    this._redisDb.Setup(s => s.StreamAcknowledgeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult((long)1));
    this._redisDb.Setup(s => s.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] { new StreamEntry("", new NameValueEntry[] { new("data", ""), new("retries", "7") }) }));

    this._inputs = new RedisInputs
    {
      Client = this._redisClient.Object,
      ConsumerGroupName = "test  cg name",
    };
  }

  public void Dispose()
  {
    this._redisClient.Reset();
    this._redisDb.Reset();
  }

  [Fact]
  public async Task GetString_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.GetString("");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task GetString_ItShouldCallStringGetAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.GetString("test key");
    this._redisDb.Verify(m => m.StringGetAsync("test key", CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task GetString_ItShouldReturnTheStringCastOfTheResultOfCallingStringGetAsyncOnTheRedisDatabase()
  {
    string expectedResult = "test string from Redis";
    this._redisDb.Setup(s => s.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue(expectedResult)));

    ICache sut = new Redis(this._inputs);

    Assert.Equal(expectedResult, await sut.GetString("test key"));
  }

  [Fact]
  public async Task GetString_IfTheResultOfCallingStringGetAsyncOnTheRedisDatabaseIsEmpty_ItShouldReturnNull()
  {
    this._redisDb.Setup(s => s.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue()));

    ICache sut = new Redis(this._inputs);

    Assert.Null(await sut.GetString("test key"));
  }

  [Fact]
  public async Task GetHash_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.GetHash("");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task GetHash_ItShouldCallHashGetAllAsyncFromTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.GetHash("some key");
    this._redisDb.Verify(m => m.HashGetAllAsync("some key", CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task GetHash_ItShouldReturnATaskThatResolvesWithADictionaryWithTheHashContents()
  {
    this._redisDb.Setup(s => s.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<HashEntry[]>([new HashEntry("key 1", "value 1"), new HashEntry("key 2", "value 2")]));

    ICache sut = new Redis(this._inputs);

    var expected = new Dictionary<string, string> {
      { "key 1", "value 1" },
      { "key 2", "value 2" },
    };
    Assert.Equal(expected, await sut.GetHash("some key"));
  }

  [Fact]
  public async Task GetHash_IfTheCallingHashGetAllAsyncFromTheRedisDatabaseReturnsAnEmptyArray_ItShouldReturnATaskThatResolvesWithNull()
  {
    this._redisDb.Setup(s => s.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<HashEntry[]>([]));

    ICache sut = new Redis(this._inputs);

    Assert.Null(await sut.GetHash("some key"));
  }

  [Fact]
  public async Task GetHash_IfTheCallingHashGetAllAsyncFromTheRedisDatabaseReturnsNull_ItShouldReturnATaskThatResolvesWithNull()
  {
    this._redisDb.Setup(s => s.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<HashEntry[]>([]));

    ICache sut = new Redis(this._inputs);

    Assert.Null(await sut.GetHash("some key"));
  }

  [Fact]
  public async Task Set_StringType_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Set("", "");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Set_StringType_ItShouldCallStringSetAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Set("test key", "test value");
    this._redisDb.Verify(m => m.StringSetAsync("test key", "test value", null, false, When.Always, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Set_StringType_IfATtlIsProvided_ItShouldCallStringSetAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Set("test key", "test value", TimeSpan.FromSeconds(5));
    this._redisDb.Verify(m => m.StringSetAsync("test key", "test value", TimeSpan.FromSeconds(5), false, When.Always, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Set_HashType_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Set("", new Dictionary<string, string>());
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Set_HashType_ItShouldCallHashSetAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    Dictionary<string, string> values = new Dictionary<string, string>() {
      { "prop1", "v1" },
      { "some key", "some value" },
    };
    HashEntry[] expected = [
      new HashEntry("prop1", "v1"),
      new HashEntry("some key", "some value"),
    ];

    await sut.Set("test key", values);
    this._redisDb.Verify(m => m.HashSetAsync("test key", expected, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Set_HashType_ItShouldReturnTrue()
  {
    ICache sut = new Redis(this._inputs);

    Assert.True(await sut.Set("", new Dictionary<string, string>()));
  }

  [Fact]
  public async Task Set_HashType_IfATtlIsProvided_ItShouldCallKeyExpireAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Set("rng key", new Dictionary<string, string>(), TimeSpan.FromMinutes(1));
    this._redisDb.Verify(m => m.KeyExpireAsync("rng key", TimeSpan.FromMinutes(1), It.IsAny<ExpireWhen>(), CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Remove_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Remove("");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Remove_ItShouldCallKeyDeleteAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Redis(this._inputs);

    await sut.Remove("test key");
    this._redisDb.Verify(m => m.KeyDeleteAsync("test key", CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Remove_IfTheCallToKeyDeleteAsyncOnTheRedisDatabaseReturnTrue_ItShouldReturnTrue()
  {
    this._redisDb.Setup(s => s.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));

    ICache sut = new Redis(this._inputs);

    Assert.True(await sut.Remove("test key"));
  }

  [Fact]
  public async Task Remove_IfTheCallToKeyDeleteAsyncOnTheRedisDatabaseReturnFalse_ItShouldReturnFalse()
  {
    this._redisDb.Setup(s => s.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(false));

    ICache sut = new Redis(this._inputs);

    Assert.False(await sut.Remove("test key"));
  }

  [Fact]
  public async Task Enqueue_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Enqueue("", new[] { "" });
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Enqueue_ItShouldCallStreamAddAsyncOnTheRedisDatabaseTheSameTimesAsTheProvidedMessages()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "test queue name";
    var testContent = "test data";
    await sut.Enqueue(expectedQName, new[] { testContent, testContent });
    this._redisDb.Verify(m => m.StreamAddAsync(
      expectedQName,
      new NameValueEntry[] {
        new("data", testContent),
        new("retries", "0"),
      },
      null, null, false, null, StreamTrimMode.KeepReferences, CommandFlags.None
    ), Times.Exactly(2));
  }

  [Fact]
  public async Task Enqueue_ItShouldReturnAnArrayWithTheValuesReceivedFromCallingStreamAddAsyncOnTheRedisDatabase()
  {
    this._redisDb.SetupSequence(s => s.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue("123")))
      .Returns(Task.FromResult(new RedisValue("456")))
      .Returns(Task.FromResult(new RedisValue("789")));

    IQueue sut = new Redis(this._inputs);

    Assert.Equal(["123", "456", "789"], await sut.Enqueue("", new[] { "", "", "" }));
  }

  [Fact]
  public async Task Dequeue_ItShouldCallStreamCreateConsumerGroupAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some queue name";
    var expectedCName = "some consumer name";
    await sut.Dequeue(expectedQName, expectedCName);
    this._redisDb.Verify(m => m.StreamCreateConsumerGroupAsync(expectedQName, "test  cg name", "0", true, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Dequeue_IfCallingStreamCreateConsumerGroupAsyncOnTheRedisDatabaseThrowsARedisServerExceptionWithAMessageContainingBusyGroupMessage_ItShouldNotThrowThatException()
  {
    this._redisDb.Setup(s => s.StreamCreateConsumerGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
      .ThrowsAsync(new RedisServerException("sdfhgsdf BUSYGROUP"));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some queue name";
    var expectedCName = "some consumer name";
    await sut.Dequeue(expectedQName, expectedCName);
  }

  [Fact]
  public async Task Dequeue_IfCallingStreamCreateConsumerGroupAsyncOnTheRedisDatabaseThrowsAnException_ItShouldThrowIt()
  {
    this._redisDb.Setup(s => s.StreamCreateConsumerGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
      .ThrowsAsync(new Exception("sdfhgsdf BUSYGROUP"));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some queue name";
    var expectedCName = "some consumer name";
    await Assert.ThrowsAsync<Exception>(() => sut.Dequeue(expectedQName, expectedCName));
  }

  [Fact]
  public async Task Dequeue_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some queue name";
    var expectedCName = "some consumer name";
    await sut.Dequeue(expectedQName, expectedCName);
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Dequeue_ItShouldCallStreamReadGroupAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedCName = "some test consumer name";
    await sut.Dequeue(expectedQName, expectedCName);
    this._redisDb.Verify(m => m.StreamReadGroupAsync(expectedQName, this._inputs.ConsumerGroupName, expectedCName, ">", 1, false, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Dequeue_ItShouldReturnTheIdAndMessageReceivedFromCallingStreamReadGroupAsyncOnTheRedisDatabase()
  {
    string expectedId = "some msg id";
    string expectedMsg = "test message";
    this._redisDb.Setup(s => s.StreamReadGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] {
        new StreamEntry(
          expectedId,
          new NameValueEntry[] {
            new("data", expectedMsg),
            new("retries", "10")
          }
        )
      }));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedCName = "some test consumer name";
    var (actualId, actualMsg) = await sut.Dequeue(expectedQName, expectedCName);
    Assert.Equal(expectedId, actualId);
    Assert.Equal(expectedMsg, actualMsg);
  }

  [Fact]
  public async Task Dequeue_IfNoMessagesAreReturnedFromCallingStreamReadGroupAsyncOnTheRedisDatabase_ItShouldReturnANullIdAndMessage()
  {
    this._redisDb.Setup(s => s.StreamReadGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] { }));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedCName = "some test consumer name";
    var (actualId, actualMsg) = await sut.Dequeue(expectedQName, expectedCName);
    Assert.Null(actualId);
    Assert.Null(actualMsg);
  }

  [Fact]
  public async Task Ack_ItShouldReturnTrue()
  {
    IQueue sut = new Redis(this._inputs);

    Assert.True(await sut.Ack("", ""));
  }

  [Fact]
  public async Task Ack_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Ack("", "");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Ack_ItShouldCallStreamAcknowledgeAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Ack(expectedQName, expectedMsgId);
    this._redisDb.Verify(m => m.StreamAcknowledgeAsync(expectedQName, this._inputs.ConsumerGroupName, expectedMsgId, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Ack_ItShouldCallStreamDeleteAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Ack(expectedQName, expectedMsgId);
    this._redisDb.Verify(m => m.StreamDeleteAsync(expectedQName, new RedisValue[] { expectedMsgId }, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Ack_IfDeletingTheKeyIsNotRequested_ItShouldNotCallStreamDeleteAsyncOnTheRedisDatabase()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Ack(expectedQName, expectedMsgId, false);
    this._redisDb.Verify(m => m.StreamDeleteAsync(expectedQName, new RedisValue[] { expectedMsgId }, CommandFlags.None), Times.Never());
  }

  [Fact]
  public async Task Ack_IfDeletingTheKeyIsNotRequested_IfNoMessagesWereAcknowledged_ItShouldNotCallStreamDeleteAsyncOnTheRedisDatabase()
  {
    this._redisDb.Setup(s => s.StreamAcknowledgeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult((long)0));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Ack(expectedQName, expectedMsgId, true);
    this._redisDb.Verify(m => m.StreamDeleteAsync(expectedQName, new RedisValue[] { expectedMsgId }, CommandFlags.None), Times.Never());
  }

  [Fact]
  public async Task Nack_ItShouldReturnTrue()
  {
    IQueue sut = new Redis(this._inputs);

    Assert.True(await sut.Nack("", "", 12));
  }

  [Fact]
  public async Task Nack_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Nack("", "", 20);
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Nack_ItShouldCallStreamRangeAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Nack(expectedQName, expectedMsgId, 30);
    this._redisDb.Verify(m => m.StreamRangeAsync(expectedQName, expectedMsgId, expectedMsgId, null, Order.Ascending, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Nack_ItShouldCallStreamAddAsyncOnTheRedisDatabaseOnce()
  {
    var expectedMsgId = "desired msg id";
    var testContent = "some content";
    this._redisDb.Setup(s => s.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] {
        new StreamEntry(
          expectedMsgId,
          new NameValueEntry[] {
            new("data", testContent),
            new("retries", "7"),
          }
        )
      }));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    await sut.Nack(expectedQName, expectedMsgId, 10);
    this._redisDb.Verify(m => m.StreamAddAsync(
      expectedQName,
      new NameValueEntry[] {
        new("data", testContent),
        new("retries", "8"),
      },
      null, null, false, null, StreamTrimMode.KeepReferences, CommandFlags.None
    ), Times.Once());
  }

  [Fact]
  public async Task Nack_ItShouldCallStreamAcknowledgeAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Nack(expectedQName, expectedMsgId, 10);
    await Task.Delay(500);
    this._redisDb.Verify(m => m.StreamAcknowledgeAsync(expectedQName, this._inputs.ConsumerGroupName, expectedMsgId, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Nack_ItShouldCallStreamDeleteAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    await sut.Nack(expectedQName, expectedMsgId, 10);
    await Task.Delay(500);
    this._redisDb.Verify(m => m.StreamDeleteAsync(expectedQName, new RedisValue[] { expectedMsgId }, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Nack_IfTheMessageHasBeenRetriedBeyondTheProvidedThreashold_ItShouldCallStreamAddAsyncOnTheRedisDatabaseForTheDlqOnce()
  {
    var expectedMsgId = "desired msg id";
    var testContent = "some content";
    this._redisDb.Setup(s => s.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] {
        new StreamEntry(
          expectedMsgId,
          new NameValueEntry[] {
            new("data", testContent),
            new("retries", "1"),
          }
        )
      }));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    await sut.Nack(expectedQName, expectedMsgId, 2);
    this._redisDb.Verify(m => m.StreamAddAsync(
      $"{expectedQName}_dlq",
      new NameValueEntry[] {
        new("data", testContent),
        new("retries", "2"),
        new("original_id", expectedMsgId),
      },
      null, null, false, null, StreamTrimMode.KeepReferences, CommandFlags.None
    ), Times.Once());
  }

  [Fact]
  public async Task Nack_IfTheMessageHasBeenRetriedBeyondTheProvidedThreashold_ItShouldReturnFalse()
  {
    var expectedMsgId = "desired msg id";
    var testContent = "some content";
    this._redisDb.Setup(s => s.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] {
        new StreamEntry(
          expectedMsgId,
          new NameValueEntry[] {
            new("data", testContent),
            new("retries", "1"),
          }
        )
      }));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    Assert.False(await sut.Nack(expectedQName, expectedMsgId, 2));
  }

  [Fact]
  public async Task Nack_IfNoMessagesAreFoundWithTheProvidedId_ItShouldThrowAnException()
  {
    this._redisDb.Setup(s => s.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] { }));

    IQueue sut = new Redis(this._inputs);

    var ex = await Assert.ThrowsAsync<Exception>(() => sut.Nack("", "some id", 12));
    Assert.Equal(
      "No message found with the provided id: 'some id'",
      ex.Message
    );
  }

  [Fact]
  public async Task Nack_IfCallingStreamAcknowledgeAsyncOnTheRedisDatabaseReturnsNoAcknowledgedMessages_ItShouldReturnTrue()
  {
    this._redisDb.Setup(s => s.StreamAcknowledgeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult((long)0));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    var expectedMsgId = "desired msg id";
    Assert.True(await sut.Nack(expectedQName, expectedMsgId, 10));
  }

  [Fact]
  public async Task Nack_IfTheMessageHasBeenRetriedBeyondTheProvidedThreashold_IfCallingStreamAcknowledgeAsyncOnTheRedisDatabaseReturnsNoAcknowledgedMessages_ItShouldReturnFalse()
  {
    var expectedMsgId = "desired msg id";
    var testContent = "some content";
    this._redisDb.Setup(s => s.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new StreamEntry[] {
        new StreamEntry(
          expectedMsgId,
          new NameValueEntry[] {
            new("data", testContent),
            new("retries", "5"),
          }
        )
      }));
    this._redisDb.Setup(s => s.StreamAcknowledgeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult((long)0));

    IQueue sut = new Redis(this._inputs);

    var expectedQName = "some test queue name";
    Assert.False(await sut.Nack(expectedQName, expectedMsgId, 6));
  }
}