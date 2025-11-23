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
        InnerInnerDoc = new TestDocumentInnerInner
        {
          SomeIntProp = 123,
        },
      },
    };

    Assert.Null(Utilities.GetByPath(testdoc, ""));
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
  public string? AnotherStrProp { get; set; }
  public string[]? AStrArray { get; set; }
  public TestDocumentInnerInner[]? ObjectArr { get; set; }
  public required TestDocumentInnerInner InnerInnerDoc { get; set; }
}

public class TestDocumentInnerInner
{
  public required int SomeIntProp { get; set; }
}