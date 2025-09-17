using System.Diagnostics.CodeAnalysis;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the LaunchDarkly SDK is done.")]
public static class FeatureFlags
{
  public static FeatureFlagsInputs PrepareInputs(
    string envSdkKey, string contextApiKey, string contextName,
    EnvNames envName, ILogger? logger = null
  )
  {
    var config = Configuration.Builder(envSdkKey)
      .StartWaitTime(TimeSpan.FromSeconds(5))
      .Offline(false)
      .Build();

    var client = new LdClient(config);

    var context = Context.Builder(contextApiKey)
      .Kind("application")
      .Name(contextName)
      .Set("env", envName.ToString())
      .Build();

    return new FeatureFlagsInputs
    {
      Client = client,
      Context = context,
      Logger = logger,
    };
  }
}