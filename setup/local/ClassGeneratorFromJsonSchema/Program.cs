using Confluent.SchemaRegistry;
using Toolkit.Utils;

internal class Program
{
  private static async Task Main(string[] args)
  {
    var topicName = Environment.GetEnvironmentVariable("KAFKA_TOPIC_NAME");
    if (string.IsNullOrWhiteSpace(topicName))
    {
      throw new Exception("❌ ERROR: KAFKA_TOPIC_NAME is not set!");
    }
    var schemaFilePath = Environment.GetEnvironmentVariable("SCHEMA_FILE_PATH");
    if (string.IsNullOrWhiteSpace(schemaFilePath))
    {
      throw new Exception("❌ ERROR: SCHEMA_FILE_PATH is not set!");
    }
    var outputPath = Environment.GetEnvironmentVariable("GENERATED_CLASSES_PATH");
    if (string.IsNullOrWhiteSpace(outputPath))
    {
      throw new Exception("❌ ERROR: GENERATED_CLASSES_PATH is not set!");
    }
    var @namespace = Environment.GetEnvironmentVariable("GENERATED_CLASSES_NAMESPACE");
    if (string.IsNullOrWhiteSpace(@namespace))
    {
      throw new Exception("❌ ERROR: GENERATED_CLASSES_NAMESPACE is not set!");
    }

    await Kafka<dynamic, dynamic>.GenerateClassesFromJsonSchema(
      topicName, schemaFilePath, outputPath, @namespace
    );

    Console.WriteLine("Classes generated successfully.");
  }
}