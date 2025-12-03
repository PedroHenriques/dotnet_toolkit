using Newtonsoft.Json;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class UtilitiesTests : IDisposable
{
  public UtilitiesTests()
  { }

  public void Dispose()
  { }

  [Fact]
  public void GetByPath_ItShouldReturnTheValueOfTheRequestedProperty()
  {
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Equal("some name", Utilities.GetByPath(testdoc, "Name"));
  }

  [Fact]
  public void GetByPath_IfThePathIsForANestedProperty_ItShouldReturnTheValueOfTheRequestedProperty()
  {
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "1",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Equal(123, Utilities.GetByPath(testdoc, "InnerDoc.InnerInnerDoc.SomeIntProp"));
  }

  [Fact]
  public void GetByPath_IfThePathIsForAnObject_ItShouldReturnTheValueOfTheRequestedProperty()
  {
    var innerInnerDoc = new TestDocumentInnerInner
    {
      SomeIntProp = 123,
    };
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "2",
        InnerInnerDoc = innerInnerDoc,
      },
    };

    Assert.Equal(innerInnerDoc, Utilities.GetByPath(testdoc, "InnerDoc.InnerInnerDoc"));
  }

  [Fact]
  public void GetByPath_IfThePathIsForAnArrayProperty_ItShouldReturnTheValueOfTheRequestedProperty()
  {
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "3",
        AStrArray = new string[]
        {
          "string 1", "string 2", "string 3"
        },
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Equal("string 3", Utilities.GetByPath(testdoc, "InnerDoc.AStrArray[2]"));
  }

  [Fact]
  public void GetByPath_IfThePathIsForAnArrayPropertyOfObjects_ItShouldReturnTheValueOfTheRequestedProperty()
  {
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "another 4",
        ObjectArr = new TestDocumentInnerInner[]
        {
          new TestDocumentInnerInner
          {
            SomeIntProp = 456,
          },
          new TestDocumentInnerInner
          {
            SomeIntProp = 789,
          }
        },
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Equal(456, Utilities.GetByPath(testdoc, "InnerDoc.ObjectArr[0].SomeIntProp"));
  }

  [Fact]
  public void GetByPath_IfThePathPropertyDoesNotExist_ItShouldReturnNull()
  {
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "5",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Null(Utilities.GetByPath(testdoc, "InnerDoc.Void.SomeIntProp"));
  }

  [Fact]
  public void GetByPath_IfTheProvidedObjectIsNull_ItShouldReturnNull()
  {
    Assert.Null(Utilities.GetByPath(null, "InnerDoc.Void.SomeIntProp"));
  }

  [Fact]
  public void GetByPath_IfTheProvidedPathIsEmpty_ItShouldReturnNull()
  {
    TestDocument testdoc = new TestDocument
    {
      Name = "some name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "yet another 6",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Null(Utilities.GetByPath(testdoc, ""));
  }

  [Fact]
  public void AddToPath_ItShouldReturnTrue()
  {
    var innerDoc = new TestDocumentInner
    {
      SomeBool = false,
      AnotherStrProp = "hello",
      InnerInnerDoc = new TestDocumentInnerInner
      {
        SomeIntProp = 123,
      },
    };
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
    };

    Assert.True(Utilities.AddToPath(testDoc, "InnerDoc", innerDoc));
  }

  [Fact]
  public void AddToPath_ItShouldAddTheValueToTheNodeInTheProvidedObject()
  {
    var innerDoc = new TestDocumentInner
    {
      SomeBool = false,
      AnotherStrProp = "hello",
      InnerInnerDoc = new TestDocumentInnerInner
      {
        SomeIntProp = 123,
      },
    };
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
    };
    TestDocument expectedDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = innerDoc,
    };

    var _ = Utilities.AddToPath(testDoc, "InnerDoc", innerDoc);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedDoc),
      JsonConvert.SerializeObject(testDoc)
    );
  }

  [Fact]
  public void AddToPath_IfThePathIsForANestedProperty_ItShouldReturnTrue()
  {
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "hello world",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.True(Utilities.AddToPath(testDoc, "InnerDoc.SomeBool", true));
  }

  [Fact]
  public void AddToPath_IfThePathIsForANestedProperty_ItShouldAddTheValueToTheNodeInTheProvidedObject()
  {
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = false,
        AnotherStrProp = "hello world",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };
    TestDocument expectedDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    var _ = Utilities.AddToPath(testDoc, "InnerDoc.SomeBool", true);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedDoc),
      JsonConvert.SerializeObject(testDoc)
    );
  }

  [Fact]
  public void AddToPath_IfThePathIsForANestedProperty_IfThatPropertyDoesNotExist_ItShouldReturnTrue()
  {
    string[] strArr = ["str 1", "str 2"];
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 2",
      },
    };

    Assert.True(Utilities.AddToPath(testDoc, "InnerDoc.AStrArray", strArr));
  }

  [Fact]
  public void AddToPath_IfThePathIsForANestedProperty_IfThatPropertyDoesNotExist_ItShouldAddTheValueToTheNodeInTheProvidedObject()
  {
    string[] strArr = ["str 1", "str 2"];
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 2",
      },
    };
    TestDocument expectedDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 2",
        AStrArray = strArr,
      },
    };

    var _ = Utilities.AddToPath(testDoc, "InnerDoc.AStrArray", strArr);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedDoc),
      JsonConvert.SerializeObject(testDoc)
    );
  }

  [Fact]
  public void AddToPath_IfThePathIsForANestedProperty_IfThatPropertyIsAClassInstance_IfThatPropertyDoesNotExist_ItShouldReturnTrue()
  {
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 2",
      },
    };

    Assert.True(Utilities.AddToPath(testDoc, "InnerDoc.InnerInnerDoc.SomeIntProp", 567));
  }

  [Fact]
  public void AddToPath_IfThePathIsForANestedProperty_IfThatPropertyIsAClassInstance_IfThatPropertyDoesNotExist_ItShouldAddTheValueToTheNodeInTheProvidedObject()
  {
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 2",
      },
    };
    TestDocument expectedDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 2",
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 567,
        },
      },
    };

    var _ = Utilities.AddToPath(testDoc, "InnerDoc.InnerInnerDoc.SomeIntProp", 567);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedDoc),
      JsonConvert.SerializeObject(testDoc)
    );
  }

  [Fact]
  public void AddToPath_IfThePathIsForAnArrayPropertyAndForASpecificIndex_ItShouldReturnTrue()
  {
    TestDocumentInnerInner innerInnerDoc = new TestDocumentInnerInner
    {
      SomeIntProp = 160,
    };
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 3",
        ObjectArr = new TestDocumentInnerInner[]
        {
          new TestDocumentInnerInner
          {
            SomeIntProp = 100,
          }
        },
      },
    };

    Assert.True(Utilities.AddToPath(testDoc, "InnerDoc.ObjectArr[0]", innerInnerDoc));
  }

  [Fact]
  public void AddToPath_IfThePathIsForAnArrayPropertyAndForASpecificIndex_ItShouldReplaceTheValueToTheNodeInTheProvidedObject()
  {
    TestDocumentInnerInner innerInnerDoc = new TestDocumentInnerInner
    {
      SomeIntProp = 160,
    };
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 3",
        ObjectArr = new TestDocumentInnerInner[]
        {
          new TestDocumentInnerInner
          {
            SomeIntProp = 100,
          }
        },
      },
    };
    TestDocument expectedDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 3",
        ObjectArr = new TestDocumentInnerInner[]
        {
          innerInnerDoc,
        },
      },
    };

    var _ = Utilities.AddToPath(testDoc, "InnerDoc.ObjectArr[0]", innerInnerDoc);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedDoc),
      JsonConvert.SerializeObject(testDoc)
    );
  }

  [Fact]
  public void AddToPath_IfThePathDoesNotExist_ItShouldReturnFalse()
  {
    TestDocumentInnerInner innerInnerDoc = new TestDocumentInnerInner
    {
      SomeIntProp = 160,
    };
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 3",
        ObjectArr = new TestDocumentInnerInner[]
        {
          new TestDocumentInnerInner
          {
            SomeIntProp = 100,
          }
        },
      },
    };

    Assert.False(Utilities.AddToPath(testDoc, "ups", innerInnerDoc));
  }

  [Fact]
  public void AddToPath_IfTheRootObjectIsNull_ItShouldThrowAnException()
  {
    ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => Utilities.AddToPath(null, "Name", ""));
    Assert.Equal("Value cannot be null. (Parameter 'root')", ex.Message);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void AddToPath_IfThePathIsNullOrEmpty_ItShouldThrowAnException(string? path)
  {
    TestDocument testDoc = new TestDocument
    {
      Name = "test name",
      InnerDoc = new TestDocumentInner
      {
        SomeBool = true,
        AnotherStrProp = "hello world 3",
      },
    };

    ArgumentException ex = Assert.Throws<ArgumentException>(() => Utilities.AddToPath(testDoc, path, "hello world"));
    Assert.Equal("Path cannot be empty. (Parameter 'path')", ex.Message);
  }
}

public class TestDocument
{
  public required string Name { get; set; }
  public string? Desc { get; set; }
  public TestDocumentInner? InnerDoc { get; set; }
}

public class TestDocumentInner
{
  public required bool SomeBool { get; set; }
  public required string AnotherStrProp { get; set; }
  public string[]? AStrArray { get; set; }
  public TestDocumentInnerInner[]? ObjectArr { get; set; }
  public TestDocumentInnerInner? InnerInnerDoc { get; set; }
}

public class TestDocumentInnerInner
{
  public required int SomeIntProp { get; set; }
}