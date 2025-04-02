using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace Toolkit.Types;

public enum EnvNames
{
  dev, qua, prd
}

public struct FeatureFlagsInputs
{
  public required ILdClient Client { get; set; }
  public required Context Context { get; set; }
}

public interface IFeatureFlags
{
  public bool GetBoolFlagValue(string flagKey);
  public void SubscribeToValueChanges(
    string flagKey,
    Action<FlagValueChangeEvent>? handler = null
  );
  public static abstract bool GetCachedBoolFlagValue(string flagKey);
}