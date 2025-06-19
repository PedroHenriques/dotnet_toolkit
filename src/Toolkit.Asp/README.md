# .Net Toolkit for Asp applications
A .Net package to facilitate interacting with the following tech stack:
- Includes the [base .Net Toolkit](../Toolkit/README.md)
- ASP.Net Middlewares

## When to use
This package is intended to be used in **Asp.Net applications**.

## Main functionalities
- Offers all the features of the base .Net Toolkit
- Has a set of Asp.Net middlewares that abstract the complexity of handling common use cases
- Reduces the cost of evolving the interaction with this tech stack across all the applications

**Note:** This package does not intend to completely abstract, from the application, the technology being used.
The application will still need to interact with some data types from the underlying technologies.

# Technical information
## Stack
This package offers functionality for the following technologies:
- Includes the [base .Net Toolkit](../Toolkit/README.md)
- ASP.Net Middlewares

## Installing these packages
```sh
dotnet add [path/to/your/csproj/file] package PJHToolkit.Asp
```

## Using this package
This package is structure by technology.<br>
Each one has a dedicated class and an associated utility function.<br>
The utility function receives basic configurations and handles the complexity of setting up the clients and other instances for that technology's SDK.<br>
The output of the utility function is then used to instanciate the class with the functionality to be used by your application.

For detailed information about each technology's class look at:
| Technology | Documentation |
| ----------- | ----------- |
| ASP.Net Middlewares | [doc](/documentation/middlewares.md) |