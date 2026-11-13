FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/GranBrain.Core/GranBrain.Core.csproj src/GranBrain.Core/
COPY src/GranBrain.Infrastructure/GranBrain.Infrastructure.csproj src/GranBrain.Infrastructure/
COPY src/GranBrain.Api/GranBrain.Api.csproj src/GranBrain.Api/
RUN dotnet restore src/GranBrain.Api/GranBrain.Api.csproj

COPY src/ src/
RUN dotnet publish src/GranBrain.Api/GranBrain.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/vault /var/run/granbrain

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "GranBrain.Api.dll"]
