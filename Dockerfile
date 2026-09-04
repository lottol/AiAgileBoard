# syntax=docker/dockerfile:1

# Build the React/Vite frontend.
FROM node:22-alpine AS frontend-build
WORKDIR /source/src/AiAgileBoard.Client

COPY src/AiAgileBoard.Client/package*.json ./
RUN npm ci

COPY src/AiAgileBoard.Client/ ./
RUN npm run build

# Restore the backend and test dependencies in a reusable SDK stage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-dependencies
WORKDIR /source

COPY Directory.Build.props ./
COPY AiAgileBoard.slnx ./
COPY src/AiAgileBoard.Api/AiAgileBoard.Api.csproj src/AiAgileBoard.Api/
COPY tests/AiAgileBoard.UnitTests/AiAgileBoard.UnitTests.csproj tests/AiAgileBoard.UnitTests/
COPY tests/AiAgileBoard.IntegrationTests/AiAgileBoard.IntegrationTests.csproj tests/AiAgileBoard.IntegrationTests/
RUN dotnet restore AiAgileBoard.slnx

COPY src/AiAgileBoard.Api/ src/AiAgileBoard.Api/

# Build this target to compile and run the complete test suite.
FROM backend-dependencies AS backend-test
COPY tests/ tests/
RUN dotnet test AiAgileBoard.slnx \
    --configuration Release \
    --no-restore

# Publish the ASP.NET Core backend with the production frontend assets.
FROM backend-dependencies AS backend-build
COPY --from=frontend-build /source/src/AiAgileBoard.Client/dist/ src/AiAgileBoard.Api/wwwroot/

RUN dotnet publish src/AiAgileBoard.Api/AiAgileBoard.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Run the application with SQLite data stored outside the application binaries.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/aiagileboard.db"

COPY --from=backend-build --chown=app:app /app/publish/ ./
RUN mkdir -p /app/data && chown app:app /app/data

USER app
VOLUME ["/app/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiAgileBoard.Api.dll"]
