# ── Stage 1 : restore + build ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + project files first (layer cache — only invalidated if .csproj changes)
COPY FleetManager.sln .
COPY src/FleetManager.Domain/FleetManager.Domain.csproj             src/FleetManager.Domain/
COPY src/FleetManager.Application/FleetManager.Application.csproj   src/FleetManager.Application/
COPY src/FleetManager.Infrastructure/FleetManager.Infrastructure.csproj src/FleetManager.Infrastructure/
COPY src/FleetManager.Api/FleetManager.Api.csproj                   src/FleetManager.Api/

RUN dotnet restore src/FleetManager.Api/FleetManager.Api.csproj

# Copy source code and publish
COPY src/ src/
RUN dotnet publish src/FleetManager.Api/FleetManager.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2 : runtime (image finale légère) ────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Utilisateur non root fourni par l'image .NET (UID 1654) : une faille dans l'API ne donne pas root.
# Seul le dossier des journaux Serilog lui est accessible en écriture.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "FleetManager.Api.dll"]
