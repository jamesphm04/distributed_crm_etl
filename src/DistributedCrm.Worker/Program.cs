using Azure.Messaging.ServiceBus;
using DistributedCrm.Application.Abstractions;
using DistributedCrm.Application.UseCases.ReturnRequests;
using DistributedCrm.Infrastructure.Caching;
using DistributedCrm.Infrastructure.ExternalApis;
using DistributedCrm.Infrastructure.HealthChecks;
using DistributedCrm.Infrastructure.Messaging;
using DistributedCrm.Infrastructure.Persistence;
using DistributedCrm.Worker;
using DistributedCrm.Worker.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

// A web host only so Container Apps can probe it: its probes are HTTP or TCP, never a command in the container.
// The health endpoints are the only HTTP surface; the real work is the hosted Worker below.
var builder = WebApplication.CreateBuilder(args);

// Read up front so a missing value stops startup with the variable's name, instead of failing on a message.
var postgresConnectionString = builder.Configuration.GetRequiredConnectionString("DistributedCrm");
var redisConnectionString = builder.Configuration.GetRequiredConnectionString("Redis");
var serviceBusConnectionString = builder.Configuration.GetRequiredConnectionString("ServiceBus");

builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseNpgsql(postgresConnectionString,
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IReturnRequestRepository, ReturnRequestRepository>();
builder.Services.AddScoped<IApprovalApiClient, SimulatedApprovalApiClient>();
builder.Services.AddScoped<IReturnRequestCacheInvalidator, RedisReturnRequestCacheInvalidator>();
builder.Services.AddScoped<ProcessReturnRequestHandler>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false; // don't throw at startup if Redis isn't ready yet - keep retrying in the background
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.Configure<SimulatedApprovalApiOptions>(
    builder.Configuration.GetSection(SimulatedApprovalApiOptions.SectionName));

// The client connects lazily and retries on its own, so there's no startup retry loop to hand-roll.
// Phase 10 swaps the connection string for AddServiceBusClientWithNamespace + managed identity.
builder.Services.AddAzureClients(clients =>
    clients.AddServiceBusClient(serviceBusConnectionString));

builder.Services.AddSingleton<ReturnRequestConsumer>();
builder.Services.AddHostedService<Worker>();

// Singleton: one receiver link for the app's lifetime instead of a new one per probe.
builder.Services.AddSingleton(sp => new ServiceBusQueueHealthCheck(
    sp.GetRequiredService<ServiceBusClient>(),
    ReturnRequestConsumer.QueueName));

// Dependency checks run only on /health/ready, never on /health/live.
const string readyTag = "ready";
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WorkerDbContext>("postgres", tags: [readyTag])
    .AddCheck<RedisHealthCheck>("redis", tags: [readyTag])
    .AddCheck<ServiceBusQueueHealthCheck>("servicebus", tags: [readyTag]);

// A hung dependency fails the probe instead of hanging it. Set here because AddDbContextCheck takes no timeout.
builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
    {
        registration.Timeout = TimeSpan.FromSeconds(5);
    }
});

var app = builder.Build();

// No auth and no forwarded headers: the worker has no ingress, only the platform's probes reach it.
// The response is only "Healthy"/"Unhealthy"; failure details go to the log.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains(readyTag) });

app.Run();
