using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the Confluent SDK is done.")]
public static class Kafka<TKey, TValue>
where TKey : class
where TValue : class
{
  public static KafkaInputs<TKey, TValue> PrepareInputs(
    SchemaRegistryConfig schemaRegistryConfig, string schemaSubject,
    int schemaVersion, ProducerConfig? producerConfig = null,
    ConsumerConfig? consumerConfig = null, IFeatureFlags? featureFlags = null
  )
  {
    ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(
      schemaRegistryConfig
    );

    IProducer<TKey, TValue>? producer = null;
    if (producerConfig != null)
    {
      producerConfig.AllowAutoCreateTopics = false;

      var jsonSerializerConfig = new JsonSerializerConfig
      {
        AutoRegisterSchemas = false,
      };

      producer = new ProducerBuilder<TKey, TValue>(producerConfig)
        .SetKeySerializer(new JsonSerializer<TKey>(schemaRegistry, jsonSerializerConfig))
        .SetValueSerializer(new JsonSerializer<TValue>(schemaRegistry, jsonSerializerConfig))
        .Build();
    }

    IConsumer<TKey, TValue>? consumer = null;
    if (consumerConfig != null)
    {
      consumerConfig.AllowAutoCreateTopics = false;
      consumerConfig.EnableAutoCommit = false;

      consumer = new ConsumerBuilder<TKey, TValue>(consumerConfig)
        .SetKeyDeserializer(new JsonDeserializer<TKey>(schemaRegistry).AsSyncOverAsync())
        .SetValueDeserializer(new JsonDeserializer<TValue>(schemaRegistry).AsSyncOverAsync())
        .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
        .Build();
    }

    return new KafkaInputs<TKey, TValue>
    {
      SchemaRegistry = schemaRegistry,
      SchemaSubject = schemaSubject,
      SchemaVersion = schemaVersion,
      Producer = producer,
      Consumer = consumer,
      FeatureFlags = featureFlags,
    };
  }

  public static async Task GenerateClassesFromJsonSchema(
    string topicName, string schemaFilePath, string outputPath, string @namespace
  )
  {
    string keySchemaFile = $"{schemaFilePath}/KeySchema.json";
    string valueSchemaFile = $"{schemaFilePath}/ValueSchema.json";

    var keyJsonSchema = await JsonSchema.FromFileAsync(keySchemaFile);
    var keyClassName = keyJsonSchema.Title ?? throw new InvalidOperationException("Key schema must have a top level title.");
    var valueJsonSchema = await JsonSchema.FromFileAsync(valueSchemaFile);
    var valueClassName = valueJsonSchema.Title ?? $"{General.ToPascalCaseSafe(topicName)}Value";

    await GenerateCSharpFromJsonSchema(
      keySchemaFile, outputPath, keyClassName, @namespace
    );
    await GenerateCSharpFromJsonSchema(
      valueSchemaFile, outputPath, valueClassName, @namespace
    );

    AddRequiredKeywordsRecursive(
      keyJsonSchema, outputPath, $"{keyClassName}.cs"
    );
    AddRequiredKeywordsRecursive(
      valueJsonSchema, outputPath, $"{valueClassName}.cs"
    );
  }

  private static async Task GenerateCSharpFromJsonSchema(
    string jsonSchemaPath, string outputPath, string className, string @namespace
  )
  {
    var schema = await JsonSchema.FromFileAsync(jsonSchemaPath);

    var settings = new CSharpGeneratorSettings
    {
      Namespace = @namespace,
      ClassStyle = CSharpClassStyle.Poco,
      PropertyNameGenerator = new PascalCasePropertyNameGenerator(),
      TypeNameGenerator = new PascalCaseTypeNameGenerator(),

      GenerateDataAnnotations = true,
      GenerateOptionalPropertiesAsNullable = true,
      RequiredPropertiesMustBeDefined = true,
    };

    var generator = new CSharpGenerator(schema, settings);
    var code = generator.GenerateFile(className);

    File.WriteAllText(Path.Combine(outputPath, $"{className}.cs"), code);
  }

  public static void AddRequiredKeywordsRecursive(
    JsonSchema rootSchema, string outputPath, string fileName
  )
  {
    var visited = new HashSet<JsonSchema>();
    var filePath = Path.Combine(outputPath, fileName);

    if (!File.Exists(filePath))
    {
      Console.WriteLine($"File {filePath} does not exist.");
      return;
    }

    var code = File.ReadAllText(filePath);
    TraverseAndPatch(rootSchema, ref code, visited);
    if (!code.Contains("using Newtonsoft.Json;"))
    {
      code = "using Newtonsoft.Json;\n" + code;
    }
    File.WriteAllText(filePath, code);
  }

  private static void TraverseAndPatch(
    JsonSchema schema, ref string code, HashSet<JsonSchema> visited
)
  {
    if (visited.Contains(schema)) return;
    visited.Add(schema);

    foreach (var p in schema.ActualProperties)
    {
      if (p.Value.IsRequired)
      {
        var propName = General.ToPascalCaseSafe(p.Key);
        Console.WriteLine($"Patching required: {propName}");

        var regex = new Regex(
            $@"(?<=\n)(\s*(?:\[[^\]]+\]\s*\n)+)?(\s*public\s+)([^\s]+)\s+{propName}\s*\{{\s*get;\s*set;\s*\}}(\s*=\s*new\s+[^\;]+;)?",
            RegexOptions.Multiline
        );

        code = regex.Replace(code, match =>
        {
          var existingAttributes = match.Groups[1].Value ?? "";
          var visibilityAndPublic = match.Groups[2].Value;
          var type = match.Groups[3].Value;
          var initializer = match.Groups[4].Value ?? "";

          if (existingAttributes.Contains("JsonRequired") || visibilityAndPublic.Contains("required"))
            return match.Value; // Already patched

          var indent = Regex.Match(visibilityAndPublic, @"^\s*").Value;

          return $"{existingAttributes}{indent}[JsonRequired]\n{indent}{visibilityAndPublic}required {type} {propName} {{ get; set; }}";
        });
      }
    }

    foreach (var property in schema.Properties.Values)
    {
      var actual = property.ActualTypeSchema;
      if (actual.Type.HasFlag(JsonObjectType.Object))
      {
        TraverseAndPatch(actual, ref code, visited);
      }
      else if (actual.Type.HasFlag(JsonObjectType.Array))
      {
        var item = actual.Item?.ActualTypeSchema;
        if (item?.Type.HasFlag(JsonObjectType.Object) == true)
        {
          TraverseAndPatch(item, ref code, visited);
        }
      }
    }
  }
}