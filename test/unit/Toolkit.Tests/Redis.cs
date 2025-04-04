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
    this._redisDb.Setup(s => s.ListMoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<ListSide>(), It.IsAny<ListSide>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue("")));
    this._redisDb.Setup(s => s.ListRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(1));
    this._redisDb.Setup(s => s.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));

    this._inputs = new RedisInputs
    {
      Client = this._redisClient.Object,
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
      .Returns(Task.FromResult<HashEntry[]?>(null));

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
  public async Task Enqueue_ItShouldCallListLeftPushAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedQName = "test queue name";
    var expectedData = new[] { "test data" };
    await sut.Enqueue(expectedQName, expectedData);
    this._redisDb.Verify(m => m.ListLeftPushAsync(expectedQName, new[] { new RedisValue("test data") }, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Enqueue_ItShouldReturnTheResultOfCallingListLeftPushAsyncOnTheRedisDatabase()
  {
    this._redisDb.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(123456789));

    IQueue sut = new Redis(this._inputs);

    Assert.Equal(123456789, await sut.Enqueue("", new[] { "" }));
  }

  [Fact]
  public async Task Dequeue_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Dequeue("some queue name");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Dequeue_ItShouldCallListMoveAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    var expectedSourceQName = "another test queue name";
    var expectedTargetQName = "another test queue name_temp";
    await sut.Dequeue(expectedSourceQName);
    this._redisDb.Verify(m => m.ListMoveAsync(expectedSourceQName, expectedTargetQName, ListSide.Right, ListSide.Left, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Dequeue_ItShouldReturnTheStringValueReceivedFromCallingListMoveAsyncOnTheRedisDatabase()
  {
    string expectedResult = "some test json string";
    this._redisDb.Setup(s => s.ListMoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<ListSide>(), It.IsAny<ListSide>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<RedisValue>(new RedisValue(expectedResult)));

    IQueue sut = new Redis(this._inputs);

    var result = await sut.Dequeue("");
    Assert.Equal(expectedResult, result);
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
  public async Task Ack_ItShouldCallListRemoveAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Ack("test q", "some value");
    this._redisDb.Verify(m => m.ListRemoveAsync("test q_temp", "some value", 0, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Ack_IfNoItemsAreRemvedFromTheList_ItShouldReturnFalse()
  {
    this._redisDb.Setup(s => s.ListRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));

    IQueue sut = new Redis(this._inputs);

    Assert.False(await sut.Ack("test q", "some value"));
  }

  [Fact]
  public async Task Nack_ItShouldReturnTrue()
  {
    IQueue sut = new Redis(this._inputs);

    Assert.True(await sut.Nack("", ""));
  }

  [Fact]
  public async Task Nack_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Nack("", "");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async Task Nack_ItShouldCallListRemoveAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Redis(this._inputs);

    await sut.Nack("test q", "some value");
    this._redisDb.Verify(m => m.ListRemoveAsync("test q_temp", "some value", 0, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task Nack_IfNoItemsAreRemvedFromTheList_ItShouldReturnFalse()
  {
    this._redisDb.Setup(s => s.ListRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));

    IQueue sut = new Redis(this._inputs);

    Assert.False(await sut.Nack("test q", "some value"));
  }
}