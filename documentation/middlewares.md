# Toolkit for ASP.Net Middlewares
## How to use
```c#
using Toolkit.Middlewares;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.UseMiddleware<CheckApiActiveMiddleware>("some feature flag key");
app.UseMiddleware<TraceIdMiddleware>("some header name", "some activity source name", "some activity name");
```

In the above snippet we:
- Start by creating an instance of `WebApplication`.
- Then we register the middlewares.

### CheckApiActiveMiddleware
The constructor of this middleware is as follows:
```c#
public CheckApiActiveMiddleware(RequestDelegate next, string featureFlagKey)
```
For each incomming request to the ASP.Net API, this middleware will check if the provided `featureFlagKey` feature flag is active by calling `GetCachedBoolFlagValue()`, on this Toolkit's `FeatureFlags` class.<br><br>
**If it is active**: the request is allowed to continue.<br><br>
**If it is inactive**: the request is blocked and a response with `503 - Service Unavailable` status code is returned.<br><br>
**NOTE:** Consult the documentation for the `FeatureFlags` service, of this Toolkit, to know more about how its `GetCachedBoolFlagValue()` method behaves.

### TraceIdMiddleware
The constructor of this middleware is as follows:
```c#
public TraceIdMiddleware(
  RequestDelegate next, ILogger<TraceIdMiddleware> logger, string traceIdHeader, string activitySourceName, string activityName
)
```
For each incomming request to the ASP.Net API, this middleware will check if the provided `traceIdHeader` is defined in the request.<br><br>
**If it is**: will call `SetTraceIds()`, on this toolkit's `Logger` class, to set the extracted `trace id`, the provided `activitySourceName` and `activityName` in the current [Activity](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity).<br>
Then the request is allowed to continue.<br>
The request is allowed to continue.<br><br>
**If it is not**: will call `SetTraceIds()`, on this toolkit's `Logger` class, to set the provided `activitySourceName` and `activityName` in the current [Activity](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity).<br>
If there is no current Activity, a new one will be created with a random trace id.<br>
The request is allowed to continue.<br><br>
**NOTE:** Consult the documentation for the `Logger` service, of this Toolkit, to know more about how its `SetTraceIds()` method behaves.