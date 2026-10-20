# syntax=docker/dockerfile:1
#
# Build context is the repository root, because this microservice is built from a
# monorepo and shares the packages/ projects with the rest of the system:
#   docker build -f apps/notification-microservice/Dockerfile .

# ---------------------------------------------------------------------------
# Base: the runtime the application needs, nothing more
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# curl is only here so the container can answer Docker's health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# ---------------------------------------------------------------------------
# Build: restore with only the project files first, so Docker can cache the
# restore layer until a dependency actually changes
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["apps/notification-microservice/src/Bizcord.NotificationService.Presentation/Bizcord.NotificationService.Presentation.csproj", "apps/notification-microservice/src/Bizcord.NotificationService.Presentation/"]
COPY ["apps/notification-microservice/src/Bizcord.NotificationService.Application/Bizcord.NotificationService.Application.csproj", "apps/notification-microservice/src/Bizcord.NotificationService.Application/"]
COPY ["apps/notification-microservice/src/Bizcord.NotificationService.Domain/Bizcord.NotificationService.Domain.csproj", "apps/notification-microservice/src/Bizcord.NotificationService.Domain/"]
COPY ["apps/notification-microservice/src/Bizcord.NotificationService.Infrastructure/Bizcord.NotificationService.Infrastructure.csproj", "apps/notification-microservice/src/Bizcord.NotificationService.Infrastructure/"]
COPY ["packages/MessageClient/src/Bizcord.MessageClient/Bizcord.MessageClient.csproj", "packages/MessageClient/src/Bizcord.MessageClient/"]
COPY ["packages/Shared.Contracts/src/Bizcord.Shared.Contracts/Bizcord.Shared.Contracts.csproj", "packages/Shared.Contracts/src/Bizcord.Shared.Contracts/"]

RUN dotnet restore "apps/notification-microservice/src/Bizcord.NotificationService.Presentation/Bizcord.NotificationService.Presentation.csproj"

# The directory structure of the monorepo is kept so the project references resolve.
COPY apps/notification-microservice/src/ apps/notification-microservice/src/
COPY packages/ packages/

WORKDIR "/src/apps/notification-microservice/src/Bizcord.NotificationService.Presentation"
RUN dotnet build "Bizcord.NotificationService.Presentation.csproj" \
    -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ---------------------------------------------------------------------------
# Publish: trimmed, ready to run output
# ---------------------------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Bizcord.NotificationService.Presentation.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# ---------------------------------------------------------------------------
# Final: runtime image, running as the non-root user shipped with the image
# ---------------------------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER $APP_UID

HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Bizcord.NotificationService.Presentation.dll"]
