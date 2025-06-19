using Toolkit.Asp.Middlewares;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests.Middlewares;

[Trait("Type", "Unit")]
public class CheckApiActiveTests : IDisposable
{
  private readonly Mock<RequestDelegate> _reqDelegateMock;
  private readonly Mock<HttpContext> _contextMock;
  private readonly Mock<HttpResponse> _responseMock;
  private readonly Mock<ILdClient> _ldClientMock;
  private readonly LaunchDarkly.Sdk.Context _testLdContext;
  private readonly FeatureFlags _testFeatureFlags;

  public CheckApiActiveTests()
  {
    this._reqDelegateMock = new Mock<RequestDelegate>(MockBehavior.Strict);
    this._contextMock = new Mock<HttpContext>(MockBehavior.Strict);
    this._responseMock = new Mock<HttpResponse>(MockBehavior.Strict);
    this._ldClientMock = new Mock<ILdClient>(MockBehavior.Strict);
    this._testLdContext = new LaunchDarkly.Sdk.Context { };

    this._reqDelegateMock.Setup(s => s.Invoke(It.IsAny<HttpContext>()))
      .Returns(Task.CompletedTask);

    this._responseMock.SetupProperty(s => s.StatusCode);
    this._contextMock.Setup(s => s.Response)
      .Returns(this._responseMock.Object);

    this._ldClientMock.Setup(m => m.BoolVariation("test flag key", this._testLdContext, It.IsAny<bool>()))
      .Returns(true);

    this._testFeatureFlags = new FeatureFlags(new FeatureFlagsInputs
    {
      Client = this._ldClientMock.Object,
      Context = this._testLdContext,
    });
    this._testFeatureFlags.GetBoolFlagValue("test flag key");
  }

  public void Dispose()
  {
    this._reqDelegateMock.Reset();
    this._contextMock.Reset();
    this._responseMock.Reset();
    this._ldClientMock.Reset();
  }

  [Fact]
  public async Task InvokeAsync_ItShouldCallTheProvidedRequestDelegateOnceWithTheHttpContextAsArgument()
  {
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object, "test flag key");

    await sut.InvokeAsync(this._contextMock.Object);

    this._reqDelegateMock.Verify(m => m.Invoke(this._contextMock.Object), Times.Once());
  }

  [Fact]
  public async Task InvokeAsync_ItShouldNotSetTheResponseStatusCode()
  {
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object, "test flag key");

    await sut.InvokeAsync(this._contextMock.Object);

    this._responseMock.Verify(m => m.StatusCode, Times.Never());
  }

  [Fact]
  public async Task InvokeAsync_IfTheFeatureFlagForTheApiBeingActiveIsFalse_ItShouldNotCallTheProvidedRequestDelegate()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("test flag key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("test flag key");
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object, "test flag key");

    await sut.InvokeAsync(this._contextMock.Object);

    this._reqDelegateMock.Verify(m => m.Invoke(It.IsAny<HttpContext>()), Times.Never());
  }

  [Fact]
  public async Task InvokeAsync_IfTheFeatureFlagForTheApiBeingActiveIsFalse_ItShouldSetTheResponseStatusCodeTo503()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("test flag key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("test flag key");
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object, "test flag key");

    await sut.InvokeAsync(this._contextMock.Object);

    Assert.Equal(503, this._responseMock.Object.StatusCode);
  }
}