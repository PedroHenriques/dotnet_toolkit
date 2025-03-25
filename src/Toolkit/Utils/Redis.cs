using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;
using Toolkit.Types;

namespace Toolkit.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to the instantiation of classes from the Redis SDK is done.")]
public class Redis
{
  public static RedisInputs PrepareInputs(ConfigurationOptions conOpts)
  {
    return new RedisInputs
    {
      Client = ConnectionMultiplexer.Connect(conOpts),
    };
  }
}