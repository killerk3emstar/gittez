FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/GitBounty.Core/GitBounty.Core.csproj src/GitBounty.Core/
COPY src/GitBounty.Infrastructure/GitBounty.Infrastructure.csproj src/GitBounty.Infrastructure/
COPY src/GitBounty.Api/GitBounty.Api.csproj src/GitBounty.Api/
RUN dotnet restore src/GitBounty.Api/GitBounty.Api.csproj

COPY src/GitBounty.Core/ src/GitBounty.Core/
COPY src/GitBounty.Infrastructure/ src/GitBounty.Infrastructure/
COPY src/GitBounty.Api/ src/GitBounty.Api/
RUN dotnet publish src/GitBounty.Api/GitBounty.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl wyłącznie dla healthchecku w compose - obraz runtime go nie zawiera
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GitBounty.Api.dll"]
