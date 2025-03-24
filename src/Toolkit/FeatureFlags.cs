using LaunchDarkly.Sdk.Server.Interfaces;
using Toolkit.Types;

namespace Toolkit;

public class FeatureFlags : IFeatureFlags
{
  private readonly FeatureFlagsInputs _inputs;

  public FeatureFlags(FeatureFlagsInputs inputs)
  {
    this._inputs = inputs;
  }

  public bool GetBoolFlagValue(string flagKey)
  {
    return this._inputs.Client.BoolVariation(flagKey, this._inputs.Context);
  }

  public void SubscribeToValueChanges(
    string flagKey,
    Action<FlagValueChangeEvent> handler
  )
  {
    this._inputs.Client.FlagTracker.FlagChanged += this._inputs.Client
      .FlagTracker.FlagValueChangeHandler(
        flagKey,
        this._inputs.Context,
        (sender, ev) => { handler(ev); }
      );
  }
}