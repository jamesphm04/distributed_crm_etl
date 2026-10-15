namespace DistributedCrm.Worker.Configuration;

public static class ConfigurationExtensions
{
    // Every environment supplies these as ConnectionStrings__<name> environment variables (appsettings.Development.json
    // only for dotnet run). Failing at startup with the variable's name beats a null reference on the first message.
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Connection string '{name}' is not configured. Set the ConnectionStrings__{name} environment variable.")
            : value;
    }
}
