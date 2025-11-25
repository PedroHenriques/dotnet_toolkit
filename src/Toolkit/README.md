# Base .Net Toolkit
A .Net package to facilitate interacting with the following tech stack:
- MongoDb
- Redis
- Kafka
- LaunchDarkly
- Logger with Opentelemetry
- Utility functionality

## When to use
This package is intended to be used in **.Net applications that are not ASP.Net**, for exemple Console Applications.

## Main functionalities
- Handles setting up the connections with MongoDb, Redis, Kafka and LaunchDarkly
- Exposes functionality to perform most operations on this tech stack while abstracting the implementation details of each technology
- Standardizes the interactions with this tech stack across all the applications that use this package
- Reduces the cost of evolving the interaction with this tech stack across all the applications

**Note:** This package does not intend to completely abstract, from the application, the technology being used.
The application will still need to interact with some data types from the underlying technologies.

# Technical information
## Stack
This package offers functionality for the following technologies:
- MongoDb
- Redis
- Kafka
- LaunchDarkly
- Opentelemetry (logging)
- Utility functionality

## Installing these packages
```sh
dotnet add [path/to/your/csproj/file] package PJHToolkit
```

## Using this package
This package is structure by technology.<br>
Each one has a dedicated class and an associated utility function.<br>
The utility function receives basic configurations and handles the complexity of setting up the clients and other instances for that technology's SDK.<br>
The output of the utility function is then used to instanciate the class with the functionality to be used by your application.

For detailed information about each technology's class look at:
| Technology | Documentation |
| ----------- | ----------- |
| MongoDb | [doc](/documentation/mongodb.md) |
| Redis | [doc](/documentation/redis.md) |
| Kafka | [doc](/documentation/kafka.md) |
| LaunchDarkly | [doc](/documentation/launchdarkly.md) |
| Logger | [doc](/documentation/logger.md) |
| Utility | [doc](/documentation/utility.md) |