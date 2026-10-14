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
FROM mcr.microsoft.com/dotnet/runtime:10.0 as runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "DistributedCrm.Worker.dll"]