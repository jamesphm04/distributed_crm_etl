# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the .csproj files first so `dotnet restore` is cached
# as its own layer, and doesn't re-run just because source code changed.
COPY src/DistributedCrm.Domain/*.csproj src/DistributedCrm.Domain/
COPY src/DistributedCrm.Application/*.csproj src/DistributedCrm.Application/
COPY src/DistributedCrm.Infrastructure/*.csproj src/DistributedCrm.Infrastructure/
COPY src/DistributedCrm.Worker/*.csproj src/DistributedCrm.Worker/
RUN dotnet restore src/DistributedCrm.Worker/DistributedCrm.Worker.csproj

# Now copy everything else and publish
COPY src/ src/
RUN dotnet publish src/DistributedCrm.Worker/DistributedCrm.Worker.csproj -c Release -o /app

# --- Runtime stage ---
# aspnet, not runtime: the worker serves its health endpoints over HTTP.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# tini as PID 1. The kernel drops a signal PID 1 sends itself unless it has a handler, so a .NET process that
# crashes (e.g. an unhandled exception at startup) aborts, never dies, and spins at 100% CPU instead of exiting.
# tini runs the app as its child, so a crash exits the container and Container Apps can restart it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

EXPOSE 8080

# Microsoft's .NET images ship a non-root "app" user (APP_UID=1654); never run the app as root.
USER $APP_UID
ENTRYPOINT ["tini", "--", "dotnet", "DistributedCrm.Worker.dll"]