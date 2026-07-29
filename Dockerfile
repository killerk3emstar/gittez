FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Gittez.Core/Gittez.Core.csproj src/Gittez.Core/
COPY src/Gittez.Infrastructure/Gittez.Infrastructure.csproj src/Gittez.Infrastructure/
COPY src/Gittez.Api/Gittez.Api.csproj src/Gittez.Api/
RUN dotnet restore src/Gittez.Api/Gittez.Api.csproj

COPY src/Gittez.Core/ src/Gittez.Core/
COPY src/Gittez.Infrastructure/ src/Gittez.Infrastructure/
COPY src/Gittez.Api/ src/Gittez.Api/
RUN dotnet publish src/Gittez.Api/Gittez.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl wyłącznie dla healthchecku w compose - obraz runtime go nie zawiera
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
# seed jest wczytywany przez DatabaseSeeder po migracjach, nie przez initdb
COPY db/seed/ db/seed/
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gittez.Api.dll"]
