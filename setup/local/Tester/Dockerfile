# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /source
COPY --link ./setup/local/Tester/Tester.csproj ./Tester/Tester.csproj
COPY --link ./src/Toolkit/Toolkit.csproj ./Toolkit/Toolkit.csproj
RUN dotnet restore ./Tester/Tester.csproj --arch x64
RUN dotnet restore ./Toolkit/Toolkit.csproj --arch x64
COPY --link ./setup/local/Tester ./Tester/
COPY --link ./src/Toolkit ./Toolkit/
RUN dotnet publish ./Tester/Tester.csproj --os linux --arch x64 --no-restore -o /app


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled
# https://www.gnu.org/software/gettext/manual/html_node/Locale-Environment-Variables.html
ENV \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
WORKDIR /app
COPY --link --from=build /app .
ENTRYPOINT ["dotnet", "Tester.dll"]