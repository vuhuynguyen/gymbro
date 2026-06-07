# syntax=docker/dockerfile:1
#
# Multi-stage build for the GymBro API (.NET 10). Build context is the gymbro/ repo root so the
# central build files (Directory.Packages.props / Directory.Build.props / nuget.config) are available.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
# Restore/publish only the API project graph (not the test project) to keep the image lean.
RUN dotnet restore Presentations/WebApi/WebApi.csproj
RUN dotnet publish Presentations/WebApi/WebApi.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Kestrel listens on 8080 (HTTP). TLS is expected to terminate at an upstream reverse proxy / load balancer.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./

# Run as the non-root user provided by the aspnet base image.
USER $APP_UID
ENTRYPOINT ["dotnet", "WebApi.dll"]
