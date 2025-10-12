# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.8] - 2025-10-12

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.3.0` to `6.3.1`.

## [4.0.7] - 2025-10-11

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.2.2` to `6.3.0`.

## [4.0.6] - 2025-10-07

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.2.1` to `6.2.2`.

## [4.0.5] - 2025-10-05

### Change

- **Dependencies**: Bumped version of `Swashbuckle.AspNetCore` from `9.0.4` to `9.0.6`.

## [4.0.4] - 2025-09-27

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.2.0` to `6.2.1`.

## [4.0.3] - 2025-09-19

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.1.1` to `6.2.0`.

## [4.0.2] - 2025-09-19

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.1.0` to `6.1.1`.

## [4.0.1] - 2025-09-18

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `6.0.0` to `6.1.0`.

## [4.0.0] - 2025-09-17

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.4.0` to `6.0.0`.

## [3.3.1] - 2025-09-14

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.3.0` to `5.4.0`.

## [3.3.0] - 2025-09-07

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.2.1` to `5.3.0`.

## [3.2.1] - 2025-09-06

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.2.0` to `5.2.1`.

## [3.2.0] - 2025-08-22

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.1.1` to `5.2.0`.

## [3.1.1] - 2025-08-16

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.1.0` to `5.1.1`.

## [3.1.0] - 2025-07-26

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.0.1` to `5.1.0`.

## [3.0.1] - 2025-07-16

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `5.0.0` to `5.0.1`.

## [3.0.0] - 2025-07-07

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `4.2.0` to `5.0.0`.

## [2.2.0] - 2025-06-28

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `4.1.0` to `4.2.0`.

## [2.1.0] - 2025-06-21

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `4.0.0` to `4.1.0`.

## [2.0.0] - 2025-06-21

### Change

- **Dependencies**: Bumped version of `base Toolkit` from `3.0.0` to `4.0.0`.

## [1.0.0] - 2025-06-19

### Added

- **Middlewares**: Add ASP middleware that checks for the value of a feature flag. If the flag is false it will block the request and return a 503 status code.
- **Middlewares**: Add ASP middleware that attempts to extract the trace id from the incomming request. If it find one will set it as the trace id for the current Activity.
