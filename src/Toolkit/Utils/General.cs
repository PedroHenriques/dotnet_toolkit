using System.Globalization;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Toolkit.Utils;

public static class General
{
  public static string ToPascalCaseSafe(string input)
  {
    var parts = input
      .Replace("-", " ")
      .Replace(".", " ")
      .Replace("_", " ")
      .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    var pascal = string.Concat(
      parts.Select(p => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p))
    );

    return pascal;
  }
}

public class PascalCasePropertyNameGenerator : IPropertyNameGenerator
{
  public string Generate(JsonSchemaProperty property)
  {
    return General.ToPascalCaseSafe(property.Name);
  }
}

public class PascalCaseTypeNameGenerator : DefaultTypeNameGenerator
{
  public override string Generate(JsonSchema schema, string typeNameHint, IEnumerable<string> reservedTypeNames)
  {
    var baseName = base.Generate(schema, typeNameHint, reservedTypeNames);
    return General.ToPascalCaseSafe(baseName);
  }
}