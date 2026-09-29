namespace FuncPulse.Core.Models;

public class AzureSettings
{
    public const string SectionName = "Azure";
    
    public bool DemoMode { get; set; }
    
    /// <summary>
    /// Timeout in seconds for Azure operations (discovery, metrics, logs).
    /// Default is 300 seconds (5 minutes) to accommodate slow networks.
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 300;
}

public class HealthThresholds
{
    public const string SectionName = "HealthThresholds";
    
    public double WarningThreshold { get; set; } = 1.0;
    public double CriticalThreshold { get; set; } = 5.0;
}
