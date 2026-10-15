using System.Diagnostics;

namespace DistributedCrm.Infrastructure.Telemetry;

// Spans for work no library traces for us. Plain System.Diagnostics, so Infrastructure takes no OpenTelemetry
// dependency; the host subscribes to ActivitySourceName.
public static class InfrastructureTelemetry
{
    public const string ActivitySourceName = "DistributedCrm.Infrastructure";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
