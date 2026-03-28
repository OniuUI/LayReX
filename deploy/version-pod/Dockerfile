# Build context: LayReX root (this folder’s grandparent: the directory that contains `LayeredChat.sln`).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Build.props
COPY src/Core/LayeredChat.Core src/Core/LayeredChat.Core
COPY src/Connectors/OpenAiCompatible/LayeredChat.Connectors.OpenAiCompatible src/Connectors/OpenAiCompatible/LayeredChat.Connectors.OpenAiCompatible
COPY samples/VersionHost samples/VersionHost
RUN dotnet publish samples/VersionHost/LayeredChat.VersionHost.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LayeredChat.VersionHost.dll"]
