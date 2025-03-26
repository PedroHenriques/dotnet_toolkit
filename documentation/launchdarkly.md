# Toolkit for LaunchDarkly
## How to use
```c#
using Toolkit;
using Toolkit.Types;
using FFUtils = Toolkit.Utils.FeatureFlags;

FeatureFlagsInputs ffInputs = FFUtils.PrepareInputs(
  "the environment sdk key",
  "the context api key",
  "context name",
  EnvNames.dev
);
IFeatureFlags featureFlags = new FeatureFlags(ffInputs);
```

In the above snippet we:
- Start by using the `FFUtils.PrepareInputs()` utility function to let the Toolkit handle all the necessary setup for interactions with LaunchDarkly.
- Then we instantiate the Toolkit's FeatureFlags class

The instance of `IFeatureFlags` exposes the following functionality:

```c#
public interface IFeatureFlags
{
  public bool GetBoolFlagValue(string flagKey);

  public void SubscribeToValueChanges(
    string flagKey,
    Action<FlagValueChangeEvent> handler
  );
}
```

### GetBoolFlagValue
Queries the current value for the provided `flagKey` feature flag, of type `boolean`.<br><br>
Throws Exceptions (generic and LaunchDarkly specific) on error.

**Example use**
```c#
string ffKey = "some-feature-flag-key";

featureFlags.GetBoolFlagValue(ffKey);
```

### SubscribeToValueChanges
Subscribes to cahnges in value for the provided `flagKey` feature flag.<br>
When its value changes, the provided `handler` callback will be invoked with information about the change.<br><br>
Throws Exceptions (generic and LaunchDarkly specific) on error.

**Example use**
```c#
string ffKey = "some-feature-flag-key";

featureFlags.SubscribeToValueChanges(
  ffKey,
  (ev) =>
  {
    if (ev.NewValue.AsBool)
    {
      // Do something now that the feature flag is TRUE
    }
    else
    {
      // Do something now that the feature flag is FALSE
    }
  }
);
```