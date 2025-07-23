using System.Diagnostics;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Tester.Topic.Models;
using Toolkit;
using Toolkit.Types;
using KafkaUtils = Toolkit.Utils.Kafka<Tester.Topic.Models.Shippingnexuskey, Tester.Topic.Models.Eopitemtrackingshippingnexusv1value>;

namespace Tester.Services;

class Kafka
{
  public Kafka(WebApplication app, MyValue document, IFeatureFlags featureFlags)
  {
    string? schemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL");
    if (schemaRegistryUrl == null)
    {
      throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");
    }
    var schemaRegistryConfig = new SchemaRegistryConfig { Url = schemaRegistryUrl };

    string? kafkaConStr = Environment.GetEnvironmentVariable("KAFKA_CON_STR");
    if (kafkaConStr == null)
    {
      throw new Exception("Could not get the 'KAFKA_CON_STR' environment variable");
    }
    var producerConfig = new ProducerConfig
    {
      BootstrapServers = kafkaConStr,
    };
    // var consumerConfig = new ConsumerConfig
    // {
    //   BootstrapServers = kafkaConStr,
    //   GroupId = "example-consumer-group",
    //   AutoOffsetReset = AutoOffsetReset.Latest,
    //   EnableAutoCommit = false,
    // };

    // var schemaRegistryConfig = new SchemaRegistryConfig
    // {
    //   Url = "https://psrc-j39np.westeurope.azure.confluent.cloud",
    //   BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
    //   BasicAuthUserInfo = "M5E6CY3VDX6MJRZT:qpZBa/nCK1CTgeBcbGHLcw3MjhsIii8uhXfVuvcjEt0R852ejvnGBkJie75ba/pc",
    // };
    // var producerConfig = new ProducerConfig
    // {
    //   BootstrapServers = "pkc-lq8gm.westeurope.azure.confluent.cloud:9092",
    //   Acks = Acks.All,
    //   SecurityProtocol = SecurityProtocol.SaslSsl,
    //   SaslMechanism = SaslMechanism.Plain,
    //   SaslUsername = "RA2BZZIE22THRUA5",
    //   SaslPassword = "oX1tAdHOMxhgJQxOL11yXkSIR79ySIKgyx/xNAQRKXKb4t748gYquTLFc8skAA3W",
    // };

    KafkaInputs<Shippingnexuskey, Eopitemtrackingshippingnexusv1value> kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, "eop.item-tracking.shipping-nexus.v1-value", 1, producerConfig, null, featureFlags
    );
    IKafka<Shippingnexuskey, Eopitemtrackingshippingnexusv1value> kafka = new Kafka<Shippingnexuskey, Eopitemtrackingshippingnexusv1value>(kafkaInputs);

    app.MapPost("/kafka", () =>
    {
      var key = new Shippingnexuskey { Id = DateTime.UtcNow.ToString() };
      var value = new Eopitemtrackingshippingnexusv1value
      {
        Metadata = new Topic.Models.Metadata
        {
          CorrelationId = ActivityTraceId.CreateRandom().ToString(),
          DataType = "10000",
          InterchangeId = Guid.NewGuid(),
          Source = "Tester",
          Timestamp = DateTime.Now,
        },
        Shipping = new Shipping
        {
          Additionals = [],
          Audit = new Audit
          {
            CreatedDatetime = DateTime.Now,
          },
          DestinAddress = "some destination address",
          DestinCountryCode = "PT",
          DestinPostalCode = "1600",
          DestinTownName = "Lisboa",
          OriginAddress = "some origin address",
          OriginCountryCode = "ES",
          OriginPostalCode = "2800",
          OriginTownName = "Faro",
          ManifestDatetime = DateTime.Now,
          Items = [
            new Items {
              ItemCode = "some item code",
            },
          ],
          ItemsCount = 1,
          RecipientName = "some recipient name",
          SenderName = "some sender name",
          ShippingCode = "some shipping code",
          ShippingDate = DateTime.Now,
          ShippingTypeCode = "some shipping type code",
          ShippingWeightDeclaredGr = 1.0,

        },
      };

      kafka.Publish(
        "eop.item-tracking.shipping-nexus.v1",
        new Message<Shippingnexuskey, Eopitemtrackingshippingnexusv1value>
        {
          Key = key,
          Value = value
        },
        (res, ex) =>
        {
          if (ex != null)
          {
            Console.WriteLine($"Event not inserted with error: {ex}");
            return;
          }
          if (res == null)
          {
            Console.WriteLine("kafka.Publish() callback invoked with NULL res.");
            return;
          }
          Console.WriteLine($"Event inserted in partition: {res.Partition} and offset: {res.Offset}.");
        }
      );
    });

    // kafka.Subscribe(
    //   ["eop.item-tracking.shipping-nexus.v1"],
    //   (res, ex) =>
    //   {
    //     if (ex != null)
    //     {
    //       Console.WriteLine($"Event not inserted with error: {ex}");
    //       return;
    //     }
    //     if (res == null)
    //     {
    //       Console.WriteLine("kafka.Subscribe() callback invoked with NULL res.");
    //       return;
    //     }
    //     Console.WriteLine($"Processing event from partition: {res.Partition} | offset: {res.Offset}");
    //     Console.WriteLine(res.Message.Key);
    //     Console.WriteLine(res.Message.Value);
    //     kafka.Commit(res);
    //   },
    //   "ctt-net-toolkit-tester-consume-kafka-events"
    // );
  }
}