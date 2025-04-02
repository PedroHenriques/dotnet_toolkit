using System.Dynamic;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Toolkit.Types;

namespace Toolkit.Tests;

[Trait("Type", "Unit")]
public class FeatureFlagsTests : IDisposable
{
  private readonly Mock<ILdClient> _clientMock;
  private readonly Mock<IFlagTracker> _flagTrackerMock;
  private readonly Mock<Action<FlagValueChangeEvent>> _handlerMock;
  private readonly Context _context;
  private readonly FeatureFlagsInputs _featureFlagsInputsInputs;

  public FeatureFlagsTests()
  {
    this._clientMock = new Mock<ILdClient>(MockBehavior.Strict);
    this._flagTrackerMock = new Mock<IFlagTracker>(MockBehavior.Strict);
    this._handlerMock = new Mock<Action<FlagValueChangeEvent>>(MockBehavior.Strict);

    this._clientMock.Setup(s => s.BoolVariation(It.IsAny<string>(), It.IsAny<Context>(), It.IsAny<bool>()))
      .Returns(false);
    this._clientMock.Setup(s => s.FlagTracker)
      .Returns(this._flagTrackerMock.Object);

    this._flagTrackerMock.Setup(s => s.FlagValueChangeHandler(It.IsAny<string>(), It.IsAny<Context>(), It.IsAny<EventHandler<FlagValueChangeEvent>>()))
      .Returns(new EventHandler<FlagChangeEvent>((s, e) => { }));

    this._handlerMock.Setup(s => s(It.IsAny<FlagValueChangeEvent>()));

    this._context = new Context();
    this._featureFlagsInputsInputs = new FeatureFlagsInputs
    {
      Client = this._clientMock.Object,
      Context = this._context,
    };
  }

  public void Dispose()
  {
    this._clientMock.Reset();
    this._handlerMock.Reset();
  }

  [Fact]
  public void GetBoolFlagValue_ItShouldCallBoolVariationFromTheClientInstanceOnceWithTheExpectedArguments()
  {
    var sut = new FeatureFlags(this._featureFlagsInputsInputs);

    sut.GetBoolFlagValue("test flag key");

    this._clientMock.Verify(m => m.BoolVariation("test flag key", this._context, false), Times.Once());
  }

  [Fact]
  public void GetBoolFlagValue_ItShouldUpdateTheFlagValueInTheFlagValuesProperty()
  {
    this._clientMock.Setup(s => s.BoolVariation(It.IsAny<string>(), It.IsAny<Context>(), It.IsAny<bool>()))
      .Returns(true);

    var sut = new FeatureFlags(this._featureFlagsInputsInputs);

    sut.GetBoolFlagValue("test flag key");
    Assert.True(FeatureFlags.GetCachedBoolFlagValue("test flag key"));
  }

  [Fact]
  public void GetBoolFlagValue_IfTheCallToBoolVariationFromTheClientInstanceReturnsTrue_ItShouldReturnTrue()
  {
    this._clientMock.Setup(s => s.BoolVariation(It.IsAny<string>(), It.IsAny<Context>(), It.IsAny<bool>()))
      .Returns(true);

    var sut = new FeatureFlags(this._featureFlagsInputsInputs);

    Assert.True(sut.GetBoolFlagValue("test flag key"));
  }

  [Fact]
  public void GetBoolFlagValue_IfTheCallToBoolVariationFromTheClientInstanceReturnsFalse_ItShouldReturnFalse()
  {
    this._clientMock.Setup(s => s.BoolVariation(It.IsAny<string>(), It.IsAny<Context>(), It.IsAny<bool>()))
      .Returns(false);

    var sut = new FeatureFlags(this._featureFlagsInputsInputs);

    Assert.False(sut.GetBoolFlagValue("test flag key"));
  }

  [Fact]
  public void SubscribeToValueChanges_ItShouldCallFlagValueChangeHandlerFromTheClientInstanceOnceWithTheExpectedArguments()
  {
    var sut = new FeatureFlags(this._featureFlagsInputsInputs);

    sut.SubscribeToValueChanges("some key", null);

    this._flagTrackerMock.Verify(m => m.FlagValueChangeHandler("some key", this._context, It.IsAny<EventHandler<FlagValueChangeEvent>>()), Times.Once());
  }

  [Fact]
  public void SubscribeToValueChanges_IfTheFunctionProvidedAs3rdArgumentIsInvoked_ItShouldUpdateTheFlagValueInTheFlagValuesProperty()
  {
    var sut = new FeatureFlags(this._featureFlagsInputsInputs);
    sut.SubscribeToValueChanges("some key", null);

    Object testSender = new ExpandoObject();
    FlagValueChangeEvent testEvent = new FlagValueChangeEvent("some key", LdValue.Null, LdValue.Of(false));
    (this._flagTrackerMock.Invocations[0].Arguments[2] as EventHandler<FlagValueChangeEvent>)(testSender, testEvent);

    Assert.False(FeatureFlags.GetCachedBoolFlagValue("some key"));
  }

  [Fact]
  public void SubscribeToValueChanges_IfTheFunctionProvidedAs3rdArgumentIsInvoked_IfAHandlerWasProvided_ItShouldCallTheHandlerProvidedAsArgumentOnceWithTheExpectedArguments()
  {
    var sut = new FeatureFlags(this._featureFlagsInputsInputs);
    sut.SubscribeToValueChanges("some key", this._handlerMock.Object);

    Object testSender = new ExpandoObject();
    FlagValueChangeEvent testEvent = new FlagValueChangeEvent("some key", LdValue.Null, LdValue.Null);
    (this._flagTrackerMock.Invocations[0].Arguments[2] as EventHandler<FlagValueChangeEvent>)(testSender, testEvent);

    this._handlerMock.Verify(m => m(testEvent), Times.Once());
  }

  [Fact]
  public void GetCachedBoolFlagValue_IfTheRequestedFlagDoesNotExistInCache_ItShouldThrowAKeyNotFoundException()
  {
    Assert.Throws<KeyNotFoundException>(() => FeatureFlags.GetCachedBoolFlagValue("fake key"));
  }
}