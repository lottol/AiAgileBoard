# syntax=docker/dockerfile:1

# Build the React/Vite frontend.
FROM node:22-alpine AS frontend-build
WORKDIR /source/src/AiAgileBoard.Client

COPY src/AiAgileBoard.Client/package*.json ./
RUN npm ci

COPY src/AiAgileBoard.Client/ ./
RUN npm run build

# Restore and publish the ASP.NET Core backend.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /source

COPY src/AiAgileBoard.Web/AiAgileBoard.Web.csproj src/AiAgileBoard.Web/
COPY src/AiAgileBoard.Application/AiAgileBoard.Application.csproj src/AiAgileBoard.Application/
COPY src/AiAgileBoard.Domain/AiAgileBoard.Domain.csproj src/AiAgileBoard.Domain/
COPY src/AiAgileBoard.Infrastructure/AiAgileBoard.Infrastructure.csproj src/AiAgileBoard.Infrastructure/
RUN dotnet restore src/AiAgileBoard.Web/AiAgileBoard.Web.csproj

COPY src/ src/
COPY --from=frontend-build /source/src/AiAgileBoard.Client/dist/ src/AiAgileBoard.Web/wwwroot/

RUN dotnet publish src/AiAgileBoard.Web/AiAgileBoard.Web.csproj \
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

ENTRYPOINT ["dotnet", "AiAgileBoard.Web.dll"]
