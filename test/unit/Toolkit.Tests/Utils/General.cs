using Moq;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Toolkit.Utils.Tests;

[Trait("Type", "Unit")]
public class GeneralTests : IDisposable
{
  public GeneralTests() { }

  public void Dispose() { }

  [Fact]
  public void General_ToPascalCaseSafe_ItShouldReturnTheInputInPascalCase()
  {
    Assert.Equal("ABcDeFghijkl", General.ToPascalCaseSafe("a-bc.de_fgHiJKl"));
  }

  [Fact]
  public void PascalCasePropertyNameGenerator_Generate_ItShouldReturnTheInputInPascalCase()
  {
    var schema = new JsonSchema();
    schema.Properties["a-bc.de_fgHiJKl"] = new JsonSchemaProperty();
    var sut = new PascalCasePropertyNameGenerator();

    Assert.Equal("ABcDeFghijkl", sut.Generate(schema.Properties["a-bc.de_fgHiJKl"]));
  }

  [Fact]
  public void PascalCaseTypeNameGenerator_Generate_ItShouldReturnTheInputInPascalCase()
  {
    var schema = new JsonSchema();
    var sut = new PascalCaseTypeNameGenerator();

    Assert.Equal("DeFghijkl", sut.Generate(schema, "a-bc.de_fgHiJKl", Enumerable.Empty<string>()));
  }
}