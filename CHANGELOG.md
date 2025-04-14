# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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