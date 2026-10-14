using DistributedCrm.Application.Abstractions;
using DistributedCrm.Application.UseCases.ReturnRequests;
using DistributedCrm.Infrastructure.Caching;
using DistributedCrm.Infrastructure.ExternalApis;
using DistributedCrm.Infrastructure.Messaging;
using DistributedCrm.Infrastructure.Persistence;
using DistributedCrm.Worker;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
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

builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
        Port = 5672
    };

    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(1);
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            ReturnRequestConsumer.DeclareTopologyAsync(connection).GetAwaiter().GetResult();
            return connection;
        }
        catch (Exception) when (attempt < maxAttempts)
        {
            Thread.Sleep(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
        }
    }
});

builder.Services.AddSingleton<ReturnRequestConsumer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Services.GetRequiredService<IConnection>(); // forces the singleton factory to run now

host.Run();


    