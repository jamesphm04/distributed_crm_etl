using Azure.Core;
using Microsoft.Azure.StackExchangeRedis;
using Npgsql;
using StackExchange.Redis;

namespace DistributedCrm.Infrastructure.Authentication;

// Mirrors EntraAuthentication in the be repo (the repos share no code by design).
// Each dependency signs in with whatever its configuration carries: a password or key when there is one (local
// compose), otherwise a Microsoft Entra token for the app's managed identity (Azure). The same image runs in both
// places, and in Azure there is no password to store or rotate.
public static class EntraAuthentication
{
    private static readonly TokenRequestContext PostgresTokenRequest =
        new(["https://ossrdbms-aad.database.windows.net/.default"]);

    // The credential caches a token until shortly before it expires, so asking every few minutes is cheap and keeps
    // a valid token ready for each new connection whatever the token's lifetime.
    private static readonly TimeSpan PostgresTokenRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PostgresTokenFailureRetryInterval = TimeSpan.FromSeconds(10);

    // A token is only checked when a connection opens, so pooled connections stay signed in after it expires.
    public static NpgsqlDataSourceBuilder UseEntraTokenWhenPasswordless(
        this NpgsqlDataSourceBuilder dataSource, TokenCredential credential)
    {
        if (dataSource.ConnectionStringBuilder.Password is not null)
        {
            return dataSource;
        }

        return dataSource.UsePeriodicPasswordProvider(
            async (_, ct) => (await credential.GetTokenAsync(PostgresTokenRequest, ct)).Token,
            PostgresTokenRefreshInterval,
            PostgresTokenFailureRetryInterval);
    }

    // The extension signs each connection in as the identity and re-authenticates it before its token expires.
    public static async Task<ConfigurationOptions> CreateRedisOptionsAsync(
        string connectionString, TokenCredential credential)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        if (options.Password is null)
        {
            await options.ConfigureForAzureWithTokenCredentialAsync(credential);
        }

        return options;
    }

    // A connection string carries its own key (the emulator); anything else is a namespace's host name.
    public static bool IsServiceBusConnectionString(string value) =>
        value.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase);
}
