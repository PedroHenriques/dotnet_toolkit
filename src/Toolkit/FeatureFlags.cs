using LaunchDarkly.Sdk.Server.Interfaces;
using Toolkit.Types;

namespace Toolkit;

public class FeatureFlags : IFeatureFlags
{
  private static readonly Dictionary<string, bool> _flagValues = [];
  private readonly FeatureFlagsInputs _inputs;

  public FeatureFlags(FeatureFlagsInputs inputs)
  {
    this._inputs = inputs;
  }

  public static bool GetCachedBoolFlagValue(string flagKey)
  {
    return _flagValues[flagKey];
  }

  public bool GetBoolFlagValue(string flagKey)
  {
    var value = this._inputs.Client.BoolVariation(flagKey, this._inputs.Context);
    _flagValues[flagKey] = value;
    return value;
  }

  public void SubscribeToValueChanges(
    string flagKey,
    Action<FlagValueChangeEvent>? handler
  )
  {
    this._inputs.Client.FlagTracker.FlagChanged += this._inputs.Client
      .FlagTracker.FlagValueChangeHandler(
        flagKey,
        this._inputs.Context,
        (sender, ev) =>
        {
          _flagValues[ev.Key] = ev.NewValue.AsBool;
          if (handler != null) { handler(ev); }
        }
      );
  }
}