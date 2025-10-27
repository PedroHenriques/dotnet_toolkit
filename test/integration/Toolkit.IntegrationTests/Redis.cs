using DbFixtures.Redis;
using Newtonsoft.Json;
using StackExchange.Redis;
using Toolkit.Types;

namespace Toolkit.Tests.Integration;

[Trait("Type", "Integration")]
public class RedisTests : IDisposable
{
  private readonly IConnectionMultiplexer _client;
  private readonly IDatabase _db;
  private readonly DbFixtures.DbFixtures _dbFixtures;
  private readonly ICache _sutCache;
  private readonly IQueue _sutQueue;

  public RedisTests()
  {
    this._client = ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
      EndPoints = { "redis:6379" },
      Ssl = false,
      AbortOnConnectFail = false,
    });
    this._db = this._client.GetDatabase(0);
    var driver = new RedisDriver(
      this._client, this._db,
      new Dictionary<string, DbFixtures.Redis.Types.KeyTypes>
      {
        { "testStr", DbFixtures.Redis.Types.KeyTypes.String },
        { "otherTestStr", DbFixtures.Redis.Types.KeyTypes.String },
        { "testHash", DbFixtures.Redis.Types.KeyTypes.Hash },
        { "otherTestHash", DbFixtures.Redis.Types.KeyTypes.Hash },
        { "testStream", DbFixtures.Redis.Types.KeyTypes.Stream },
        { "testStream_dlq", DbFixtures.Redis.Types.KeyTypes.Stream },
        { "otherTestStream", DbFixtures.Redis.Types.KeyTypes.Stream },
        { "otherTestStream_dlq", DbFixtures.Redis.Types.KeyTypes.Stream },
      }
    );
    this._dbFixtures = new DbFixtures.DbFixtures([driver]);

    this._sutCache = new Redis(
      new RedisInputs
      {
        Client = this._client,
        ConsumerGroupName = "test consumer group",
      }
    );
    this._sutQueue = (IQueue)this._sutCache;
  }

  public void Dispose()
  {
    this._dbFixtures.CloseDrivers();
  }

  [Fact]
  public async Task GetString_ItShouldReturnTheValueOfTheStringKey()
  {
    await this._dbFixtures.InsertFixtures<string>(
      ["testStr", "otherTestStr"],
      new Dictionary<string, string[]>
      {
        { "testStr", [ "some test value" ] },
        { "otherTestStr", [ "some other test value" ] },
      }
    );

    Assert.Equal(
      "some test value",
      await this._sutCache.GetString("testStr")
    );
  }

  [Fact]
  public async Task GetHash_ItShouldReturnTheValueOfTheHashKey()
  {
    var expectedValues = new Dictionary<string, string> {
      { "hash k1", "hash v1" },
      { "hash k2", "hash v2" },
    };

    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testHash", "otherTestHash"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        { "testHash", [ expectedValues ] },
        {
          "otherTestHash",
          [
            new Dictionary<string, string> {
              { "other hash k1", "other hash v1" },
            }
          ]
        },
      }
    );

    Assert.Equal(
      expectedValues,
      await this._sutCache.GetHash("testHash")
    );
  }

  [Fact]
  public async Task Set_WithStringValue_ItShouldInsertTheStringValueInTheStringKey()
  {
    await this._dbFixtures.InsertFixtures<string>(
      ["testStr", "otherTestStr"],
      new Dictionary<string, string[]>
      {
        { "testStr", [ "some test value" ] },
        { "otherTestStr", [ "some other test value" ] },
      }
    );

    await this._sutCache.Set("testStr", "some new test value");

    Assert.Equal(
      "some new test value",
      this._db.StringGet("testStr")
    );
  }

  [Fact]
  public async Task Set_WithDictionaryValue_ItShouldInsertTheDictionaryValueInTheHashKey()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testHash", "otherTestHash"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testHash",
          [
            new Dictionary<string, string> {
              { "hash k1", "hash v1" },
              { "hash k2", "hash v2" },
            }
          ]
        },
        {
          "otherTestHash",
          [
            new Dictionary<string, string> {
              { "other hash k1", "other hash v1" },
            }
          ]
        },
      }
    );

    var expectedValues = new Dictionary<string, string> {
      { "other hash k1", "other hash v1" },
      { "new other hash k1", "new other hash v1" },
    };
    await this._sutCache.Set("otherTestHash", expectedValues);

    Assert.Equal(
      expectedValues,
      this._db.HashGetAll("otherTestHash").ToStringDictionary()
    );
  }

  [Fact]
  public async Task Remove_ItShouldDeleteTheKey()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testHash", "otherTestHash"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testHash",
          [
            new Dictionary<string, string> {
              { "hash k1", "hash v1" },
              { "hash k2", "hash v2" },
            }
          ]
        },
        {
          "otherTestHash",
          [
            new Dictionary<string, string> {
              { "other hash k1", "other hash v1" },
            }
          ]
        },
      }
    );

    await this._sutCache.Remove("otherTestHash");

    Assert.Empty(this._db.HashGetAll("otherTestHash"));
  }

  [Fact]
  public async Task Enqueue_ItShouldInsertTheMessageInTheKey()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "otherTestStream"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              { "col 1", "value 1" },
              { "col 2", "value 2" },
            },
          ]
        },
        { "otherTestStream", [ ] },
      }
    );

    var ids = await this._sutQueue.Enqueue("testStream", ["sut message"]);

    var streamMsgs = this._db.StreamRange("testStream");
    var actualMsgs = streamMsgs.Select(msg => msg.Values);

    NameValueEntry[][] expectedMsgs = [
      [
        new NameValueEntry("col 1", "value 1"),
        new NameValueEntry("col 2", "value 2"),
      ],
      [
        new NameValueEntry("data", "sut message"),
      ],
    ];

    Assert.Single(ids);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
    );
  }

  [Fact]
  public async Task Enqueue_IfATtlIsProvided_ItShouldInsertTheMessageInTheKeyAndDeleteMessagesOlderThanTheTtl()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "otherTestStream"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              { "col 1", "value 1" },
              { "col 2", "value 2" },
            },
          ]
        },
        { "otherTestStream", [ ] },
      }
    );

    var ids = await this._sutQueue.Enqueue("testStream", ["sut message"], TimeSpan.FromSeconds(5));

    var streamMsgs = this._db.StreamRange("testStream");
    var actualMsgs = streamMsgs.Select(msg => msg.Values);

    NameValueEntry[][] expectedMsgs = [
      [
        new NameValueEntry("col 1", "value 1"),
        new NameValueEntry("col 2", "value 2"),
      ],
      [
        new NameValueEntry("data", "sut message"),
      ],
    ];

    Assert.Single(ids);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
    );

    await Task.Delay(5000);

    ids = await this._sutQueue.Enqueue("testStream", ["another sut message"], TimeSpan.FromSeconds(5));

    streamMsgs = this._db.StreamRange("testStream");
    actualMsgs = streamMsgs.Select(msg => msg.Values);

    expectedMsgs = [
      [
        new NameValueEntry("data", "another sut message"),
      ],
    ];

    Assert.Single(ids);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
    );
  }

  [Fact]
  public async Task Dequeue_ItShouldConsumeTheMessagesInTheKey()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "otherTestStream"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 1" },
                  { "col 2", "value 2" },
                })
              },
              { "retries", "0" },
            },
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 3" },
                })
              },
              { "retries", "1" },
            },
          ]
        },
        {
          "otherTestStream",
          [
            new Dictionary<string, string> {
              { "other col 1", "other value 1" },
            },
          ]
        },
      }
    );

    var (id1, message1) = await this._sutQueue.Dequeue("testStream", "some rng group");
    Assert.NotNull(id1);
    Assert.Equal(
      JsonConvert.SerializeObject(new Dictionary<string, string> {
        { "col 1", "value 1" },
        { "col 2", "value 2" },
      }),
      message1
    );

    var (id2, message2) = await this._sutQueue.Dequeue("testStream", "some rng group");
    Assert.NotNull(id2);
    Assert.Equal(
      JsonConvert.SerializeObject(new Dictionary<string, string> {
        { "col 1", "value 3" },
      }),
      message2
    );

    var (id3, message3) = await this._sutQueue.Dequeue("testStream", "some rng group");
    Assert.Null(id3);
    Assert.Null(message3);
  }

  [Fact]
  public async Task Ack_ItShouldMarkTheMessageAsConsumedInTheKey()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "otherTestStream"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 1" },
                  { "col 2", "value 2" },
                })
              },
              { "retries", "0" },
            },
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 3" },
                })
              },
              { "retries", "1" },
            },
          ]
        },
        {
          "otherTestStream",
          [
            new Dictionary<string, string> {
              { "other col 1", "other value 1" },
            },
          ]
        },
      }
    );

    var (id1, _) = await this._sutQueue.Dequeue("testStream", "some rng group");
    await this._sutQueue.Ack("testStream", id1, false);
    var (id2, _) = await this._sutQueue.Dequeue("testStream", "some rng group");
    await this._sutQueue.Ack("testStream", id2, false);

    var pendingInfo = await this._db.StreamPendingAsync("testStream", "test consumer group");
    Assert.Equal(0, pendingInfo.PendingMessageCount);
  }

  [Fact]
  public async Task Ack_IfItWasRequestedThatTheAcknowledgedMessageBeDeleted_ItShouldDeleteTheMessageFromTheKey()
  {
    var expectedMsgData = JsonConvert.SerializeObject(new Dictionary<string, string> {
      { "col 1", "value 3" },
    });
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "otherTestStream"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 1" },
                  { "col 2", "value 2" },
                })
              },
              { "retries", "0" },
            },
            new Dictionary<string, string> {
              { "data", expectedMsgData },
              { "retries", "1" },
            },
          ]
        },
        {
          "otherTestStream",
          [
            new Dictionary<string, string> {
              { "other col 1", "other value 1" },
            },
          ]
        },
      }
    );

    var (id1, _) = await this._sutQueue.Dequeue("testStream", "some rng group");
    await this._sutQueue.Ack("testStream", id1, true);
    var (id2, _) = await this._sutQueue.Dequeue("testStream", "some rng group");
    await this._sutQueue.Ack("testStream", id2, false);

    var pendingInfo = await this._db.StreamPendingAsync("testStream", "test consumer group");
    Assert.Equal(0, pendingInfo.PendingMessageCount);

    var streamMsgs = this._db.StreamRange("testStream");
    var actualMsgs = streamMsgs.Select(msg => msg.Values);

    NameValueEntry[][] expectedMsgs = [
      [
        new NameValueEntry("data", expectedMsgData),
        new NameValueEntry("retries", 1),
      ],
    ];

    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
    );
  }

  [Fact]
  public async Task Nack_ItShouldParkTheMessageInADedicatedConsumerWithTheRetryCountIncreased()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "testStream_dlq", "otherTestStream", "otherTestStream_dlq"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 1" },
                  { "col 2", "value 2" },
                })
              }
            },
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 3" },
                })
              }
            },
          ]
        },
        {
          "otherTestStream",
          [
            new Dictionary<string, string> {
              { "other col 1", "other value 1" },
            },
          ]
        },
        { "testStream_dlq", [] },
        { "otherTestStream_dlq", [] },
      }
    );

    var (id1, _) = await this._sutQueue.Dequeue("testStream", "some rng group");
    var retrying = await this._sutQueue.Nack("testStream", id1, 100, "some rng group");
    await Task.Delay(500);

    var streamMsgs = this._db.StreamRange("testStream");
    var actualMsgs = streamMsgs.Select(msg => msg.Values);

    NameValueEntry[][] expectedMsgs = [
      [
        new NameValueEntry(
          "data",
          JsonConvert.SerializeObject(new Dictionary<string, string> {
            { "col 1", "value 1" },
            { "col 2", "value 2" },
          })
        )
      ],
      [
        new NameValueEntry(
          "data",
          JsonConvert.SerializeObject(new Dictionary<string, string> {
            { "col 1", "value 3" },
          })
        )
      ],
    ];

    Assert.True(retrying);
    var pendingInfo = await this._db.StreamPendingAsync("testStream", "test consumer group");
    Assert.Equal(1, pendingInfo.PendingMessageCount);
    var pendingMsgInfo = await this._db.StreamPendingMessagesAsync(
      "testStream", "test consumer group", 1, "parkingConsumer", id1, id1, CommandFlags.None
    );
    Assert.Single(pendingMsgInfo);
    Assert.Equal(1, pendingMsgInfo[0].DeliveryCount);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
    );
  }

  [Fact]
  public async Task Nack_IfTheRetryThresholdIsReached_ItShouldInsertTheMessageInTheDLQStreamKey()
  {
    await this._dbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "testStream_dlq", "otherTestStream", "otherTestStream_dlq"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "testStream",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 1" },
                  { "col 2", "value 2" },
                })
              },
            },
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new Dictionary<string, string> {
                  { "col 1", "value 3" },
                })
              },
            },
          ]
        },
        {
          "otherTestStream",
          [
            new Dictionary<string, string> {
              { "other col 1", "other value 1" },
            },
          ]
        },
        { "testStream_dlq", [] },
        { "otherTestStream_dlq", [] },
      }
    );

    var (id1, _) = await this._sutQueue.Dequeue("testStream", "some rng group");
    var retrying = await this._sutQueue.Nack("testStream", id1, 0, "some rng group");
    await Task.Delay(500);

    var streamMsgs = this._db.StreamRange("testStream");
    var actualMsgs = streamMsgs.Select(msg => msg.Values);
    var dlqDtreamMsgs = this._db.StreamRange("testStream_dlq");
    var dlqActualMsgs = dlqDtreamMsgs.Select(msg => msg.Values);

    NameValueEntry[][] expectedMsgs = [
      [
        new NameValueEntry(
          "data",
          JsonConvert.SerializeObject(new Dictionary<string, string> {
            { "col 1", "value 3" },
          })
        ),
      ],
    ];
    NameValueEntry[][] dlqExpectedMsgs = [
      [
        new NameValueEntry(
          "data",
          JsonConvert.SerializeObject(new Dictionary<string, string> {
            { "col 1", "value 1" },
            { "col 2", "value 2" },
          })
        ),
        new NameValueEntry("retries", 1),
        new NameValueEntry("original_id", id1),
      ],
    ];

    Assert.False(retrying);
    var pendingInfo = await this._db.StreamPendingAsync("testStream", "test consumer group");
    Assert.Equal(0, pendingInfo.PendingMessageCount);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
    );
    Assert.Equal(
      JsonConvert.SerializeObject(dlqExpectedMsgs),
      JsonConvert.SerializeObject(dlqActualMsgs)
    );
  }
}