using System.Net;

namespace Toolkit.Asp.Middlewares;

public class CheckApiActiveMiddleware
{
  private readonly RequestDelegate _next;
  private readonly string _featureFlagKey;

  public CheckApiActiveMiddleware(RequestDelegate next, string featureFlagKey)
  {
    this._next = next;
    this._featureFlagKey = featureFlagKey;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (FeatureFlags.GetCachedBoolFlagValue(this._featureFlagKey))
    {
      await this._next.Invoke(context);
    }
    else
    {
      context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
    }
  }
}