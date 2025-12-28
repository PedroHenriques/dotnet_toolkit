using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace Toolkit.Types;

public enum EnvNames
{
  local, dev, qua, prd
}

public struct FeatureFlagsInputs
{
  public required ILdClient Client { get; set; }
  public required Context Context { get; set; }
  public ILogger? Logger { get; set; }
}

public interface IFeatureFlags
{
  public bool GetBoolFlagValue(string flagKey, bool defaultValue = false);
  public void SubscribeToValueChanges(
    string flagKey,
    Action<FlagValueChangeEvent>? handler = null
  );
  public static virtual bool GetCachedBoolFlagValue(string flagKey) => throw new NotImplementedException();
}
