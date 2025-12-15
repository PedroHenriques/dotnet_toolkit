# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [8.4.0] - 2025-12-15

### Added

- **Redis**: Add the `Subscribe` methods to facilitate performing long polling to a Redis queue, using either a cancellation token or a feature flag key.

### Change

- **Redis**: `RedisInputs` now accepts a Feature Flag instance.

## [8.3.0] - 2025-12-08

### Added

- **Kafka**: `Publish` now handles adding the current Activity's trace ID to the published message.
- **Redis**:
  - `Enqueue`: Now stores the current Activity's trace ID in the message added to the stream. It is added in an internal property that is not visible to the application.
  - `Dequeue`: If the dequeued message has a stored trace ID, then the current Activity will be set to use that trace ID. A log will be generated if the trace ID is not valid.
- **Utilities**: Add `AddToPath` method which facilitates adding properties to objects.

### Change

- **Redis**: `RedisInputs` now accepts Logger instance and Activity related information.

### Fixed

- **Kafka**: `Subscribe` has swapped the use of `activity name` and `activity source name` when setting a trace ID extracted from a consumed event.

## [8.2.1] - 2025-11-29

### Fixed

- **Logger**: Fix issue with `PrepareInputs(IHostApplicationBuilder builder)` where an activity listener was not being started. This caused some cases where setting a Trace ID wouldn't be properly propagated.

### Change

- **Dependency Updates**:
  - Bump `MongoDB.Driver` version from `3.5.0` to `3.5.2`.

## [8.2.0] - 2025-11-25

### Added

- **Utilities**: Add the `Utilities` static class containing utility functionality, starting with object path search.

### Change

- **Logger**: `SetTraceIds` now handles cases where the provided trace id isn't valid by generating a random valid trace id.
- **Kafka**: `Subscribe` now handles extracting a trace ID from the consumed message and setting it in the Logger activity.

## [8.1.0] - 2025-11-17

### Added

- **Logger**: Add environment variable that allows controlling how the log exporters push logs.

## [8.0.1] - 2025-11-15

### Change

- **Dependency Updates**:
  - Bump `LaunchDarkly.ServerSdk` version from `8.10.3` to `8.10.4`.
  - Bump `OpenTelemetry.Exporter.Console` version from `1.13.1` to `1.14.0`.
  - Bump `OpenTelemetry.Exporter.OpenTelemetryProtocol` version from `1.13.1` to `1.14.0`.
  - Bump `OpenTelemetry.Extensions.Hosting` version from `1.13.1` to `1.14.0`.

## [8.0.0] - 2025-11-08

### Change

- **Mongodb**:
  - `WatchDb`: Now yields metadata events, besides the data events, to give more visibility over the health of the stream.
  - `WatchDb`: Receives an optional argument with the batch size, which determine how many changes will be pulled from the database in 1 batch.

## [7.0.0] - 2025-10-27

### Added

- **Redis**:
  - `Dequeue`: Adjust logic to auto claim messages after the provided visibility timeout
  - `Nack`: Change logic to park message for the duration of the visibility timeout, instead of enqueueing a new message with the same content

## [6.5.1] - 2025-10-27

### Added

- **Mongodb**: Change the order of the sort operation in `Find()`'s pipeline to better apply when a distinct document filter is applied.

## [6.5.0] - 2025-10-23

### Added

- **Mongodb**: Add support for counting values in specific fields of documents.

## [6.4.0] - 2025-10-17

### Added

- **Mongodb**: Add support for querying unique documents based on a specific field.

## [6.3.1] - 2025-10-11

### Change

- **Dependency Updates**:
  - Bump `Confluent.Kafka` version from `2.11.1` to `2.12.0`.
  - Bump `Confluent.SchemaRegistry.Serdes.Avro` version from `2.11.1` to `2.12.0`.
  - Bump `Confluent.SchemaRegistry.Serdes.Json` version from `2.11.1` to `2.12.0`.
  - Bump `OpenTelemetry.Exporter.Console` version from `1.13.0` to `1.13.1`.
  - Bump `OpenTelemetry.Exporter.OpenTelemetryProtocol` version from `1.13.0` to `1.13.1`.
  - Bump `OpenTelemetry.Extensions.Hosting` version from `1.13.0` to `1.13.1`.

## [6.3.0] - 2025-10-07

### Added

- **Kafka**: Add support for publishing and consuming from topics with AVRO schemas.

## [6.2.2] - 2025-10-05

### Change

- **Dependency Updates**:
  - Bump `LaunchDarkly.ServerSdk` version from `8.10.2` to `8.10.3`.
  - Bump `OpenTelemetry.Exporter.Console` version from `1.12.0` to `1.13.0`.
  - Bump `OpenTelemetry.Exporter.OpenTelemetryProtocol` version from `1.12.0` to `1.13.0`.
  - Bump `OpenTelemetry.Extensions.Hosting` version from `1.12.0` to `1.13.0`.

## [6.2.1] - 2025-09-27

### Change

- **Dependency Updates**: Bump `LaunchDarkly.ServerSdk` version from 8.10.1` to `8.10.2`.

## [6.2.0] - 2025-09-20

### Change

- **Mongodb**: Add support for providing an optional sort document to `Find()` method.

## [6.1.1] - 2025-09-18

### Change

- **Dependency Updates**:
  - Bump `Newtonsoft.Json` version from `13.0.3` to `13.0.4`.
  - Bump `NRedisStack` version from `1.1.0` to `1.1.1`.

## [6.1.0] - 2025-09-17

### Change

- **FeatureFlags**: Add logic to fetch current key value and populate cache, when calling to subscribe to value changes.
- **FeatureFlags**: Add support for logging flag value changes, when subscribing to key value changes, if an instance of `ILogger` is provided.

## [6.0.0] - 2025-09-16

### Change

- **Kafka**: Removed no longer used arguments from Kafka service.

## [5.4.0] - 2025-09-14

### Added

- **Redis**: Add support for passing a TTL to the `IQueue - Enqueue` operation, which will delete all messages older then the provided TTL.

## [5.3.1] - 2025-09-13

### Change

- **Dependency Updates**: Bump `MongoDB.Driver` version from `3.4.3` to `3.5.0`.

## [5.3.0] - 2025-09-07

### Added

- **Mongodb**: Add support to defining the name of the property that will be used  to identify a soft deleted document.

## [5.2.1] - 2025-09-06

### Added

- **FeatureFlags**: Add `local` to the supported env names config enum.
- **Dependencies**: Dependencies version bumps.

## [5.2.0] - 2025-08-22

### Added

- **Logger**: Add support for structured log placeholders, via `Log()`, in the standalone logger.
- **Logger**: Add support for scopes, via `BeginScope()`, in the standalone logger.

## [5.1.1] - 2025-08-15

### Fixed

- **Redis**: Fixed typo in argument of `Redis.Nack()` method.

## [5.1.0] - 2025-07-26

### Change

- **Logger**: Now exports logs to an `otel exporter` and the `console`, instead of exporting via tcp to `logstash`.
- **Logger**: Now required the `LOG_DESTINATION_URI` environment variable.

# [5.0.1] - 2025-07-16

### Fixed

- **Mongodb**: Fix scenario where the call to `UpdateOne()` and `UpdateMany()` with the upsert option set to true was throwing an Exception, even though the document was inserted.

## [5.0.0] - 2025-07-07

### Change

- **Kafka**: Force the following settings in the Kafka Producer generated by the `PrepareInputs()` util method:
  - `AllowAutoCreateTopics`: False
- **Kafka**: Force the following settings in the Kafka Consumer generated by the `PrepareInputs()` util method:
  - `AllowAutoCreateTopics`: False
  - `EnableAutoCommit`: False

## [4.1.0] - 2025-06-28

### Added

- **MongoDb**: Add functionality to update documents.

### Change

- **Redis**: `Nack()` now returns `true` if the message was requeued or `false` if the message was sent to the DLQ.

## [4.0.1] - 2025-06-22

### Change

- **Kafka**: Add serialization configuration, to the Kafka producer builder, to mitigate limitation with Confluent SDK and JSON schemas being limited to draft-04.

## [4.0.0] - 2025-06-21

### Change

- **Kafka**: Change the schema of the handler received by the `Publish` and `Subscribe` functionality to have 2 arguments (The previous one and an Exception). The handler will now by invoked in case of errors publishing or consuming events from topics.

## [3.0.0] - 2025-06-19

- This version is identical to version 2.3.0 and was deployed by mistake in the CI/CD pipeline.

## [2.3.0] - 2025-06-19

### Removed

- **Middlewares**: Removed the 2 middlewares. They are now in a dedicated package (PJHToolkit.Asp).

## [2.2.0] - 2025-06-19

### Added

- **Middlewares**: Add ASP middleware that checks for the value of a feature flag. If the flag is false it will block the request and return a 503 status code.
- **Middlewares**: Add ASP middleware that attempts to extract the trace id from the incomming request. If it find one will set it as the trace id for the current Activity.

## [2.1.0] - 2025-06-04

### Added

- **Logger**: Add Logger functionality, using Opentelemetry standards.

## [2.0.0] - 2025-05-26

### Change

- **Queue**: Change the handling of queues from Redis lists to Redis streams.

## [1.3.3] - 2025-05-19

### Change

- **MongoDb**: Improve parsing of Bson Documents, received via Mongo Stream, to better map to .Net primitive data types.

## [1.3.2] - 2025-05-07

### Change

- **Kafka**: Improve resilience of event serialization and deserialization linked to the Schema Registry.

## [1.3.0] - 2025-04-15

### Added

- **Kafka**: Overload for the `Subscribe` method that accepts a feature flag key, instead of a cancellation token.

## [1.2.1] - 2025-04-14

### Changed

- IFeatureFlags interface to have the static method GetCachedBoolFlagValue be virtual, instead of abstract. This allows using the interface as type declaration.

## [1.2.0] - 2025-04-02

### Added

- State management for the feature flag keys that are requested for the current value or for a value change subscription.

## [1.1.0] - 2025-03-31

### Added

- Support to stop listening to a MongoDb Stream, by adding an optional argument to WatchDb() for a CancellationToken.

## [1.0.0] - 2025-03-24

### Added

- Initial version of the application
