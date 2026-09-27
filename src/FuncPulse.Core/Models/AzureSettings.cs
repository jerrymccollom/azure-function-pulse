namespace FuncPulse.Core.Models;

public class AzureSettings
{
    public const string SectionName = "Azure";
    
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public bool DemoMode { get; set; }
}

public class HealthThresholds
{
    public const string SectionName = "HealthThresholds";
    
    public double WarningThreshold { get; set; } = 1.0;
    public double CriticalThreshold { get; set; } = 5.0;
}
