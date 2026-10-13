namespace DistributedCrm.Infrastructure.ExternalApis;

public class SimulatedApprovalApiOptions
{
    public const string SectionName = "SimulatedApprovalApi";
    public double FailureRate { get; set; } = 0.3;
    public double ApprovalRate { get; set; } = 0.7;
    public int MinLatencyMs { get; set; } = 200;
    public int MaxLatencyMs { get; set; } = 1500;
}