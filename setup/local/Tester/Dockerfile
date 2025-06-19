# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /source
COPY --link ./setup/local/Tester/Tester.csproj ./Tester/Tester.csproj
COPY --link ./src/Toolkit/Toolkit.csproj ./Toolkit/Toolkit.csproj
COPY --link ./src/Toolkit.Asp/Toolkit.Asp.csproj ./Toolkit.Asp/Toolkit.Asp.csproj
RUN dotnet restore ./Tester/Tester.csproj --arch x64 -p:UseLocalToolkit=true
RUN dotnet restore ./Toolkit/Toolkit.csproj --arch x64 -p:UseLocalToolkit=true
RUN dotnet restore ./Toolkit.Asp/Toolkit.Asp.csproj --arch x64 -p:UseLocalToolkit=true
COPY --link ./setup/local/Tester ./Tester/
COPY --link ./src/Toolkit ./Toolkit/
COPY --link ./src/Toolkit.Asp ./Toolkit.Asp/
RUN dotnet publish ./Tester/Tester.csproj --os linux --arch x64 --no-restore -o /app -p:UseLocalToolkit=true


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled
# https://www.gnu.org/software/gettext/manual/html_node/Locale-Environment-Variables.html
ENV \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
WORKDIR /app
COPY --link --from=build /app .
ENTRYPOINT ["dotnet", "Tester.dll"]