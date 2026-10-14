using DistributedCrm.Application.Abstractions;
using DistributedCrm.Application.UseCases.ReturnRequests;
using DistributedCrm.Infrastructure.Caching;
using DistributedCrm.Infrastructure.ExternalApis;
using DistributedCrm.Infrastructure.Messaging;
using DistributedCrm.Infrastructure.Persistence;
using DistributedCrm.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DistributedCrm"),
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IReturnRequestRepository, ReturnRequestRepository>();
builder.Services.AddScoped<IApprovalApiClient, SimulatedApprovalApiClient>();
builder.Services.AddScoped<IReturnRequestCacheInvalidator, RedisReturnRequestCacheInvalidator>();
builder.Services.AddScoped<ProcessReturnRequestHandler>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
    options.AbortOnConnectFail = false; // don't throw at startup if Redis isn't ready yet - keep retrying in the background
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.Configure<SimulatedApprovalApiOptions>(
    builder.Configuration.GetSection(SimulatedApprovalApiOptions.SectionName));

// The client connects lazily and retries on its own, so there's no startup retry loop to hand-roll.
// Phase 10 swaps the connection string for AddServiceBusClientWithNamespace + managed identity.
builder.Services.AddAzureClients(clients =>
    clients.AddServiceBusClient(builder.Configuration.GetConnectionString("ServiceBus")));

builder.Services.AddSingleton<ReturnRequestConsumer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
