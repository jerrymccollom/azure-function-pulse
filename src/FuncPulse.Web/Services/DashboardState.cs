using FuncPulse.Core.Models;

namespace FuncPulse.Web.Services;

public sealed class DashboardState
{
    private DashboardSnapshot? _snapshot;

    public bool TryRestore(string subscriptionId, out DashboardSnapshot snapshot)
    {
        if (_snapshot == null ||
            !string.Equals(_snapshot.SubscriptionId, subscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            snapshot = default!;
            return false;
        }

        snapshot = _snapshot;
        return true;
    }

    public void Capture(
        string subscriptionId,
        string resourceGroup,
        TimeRange selectedTimeRange,
        IEnumerable<string> availableResourceGroups,
        IEnumerable<FunctionAppInfo> functionApps)
    {
        _snapshot = new DashboardSnapshot(
            subscriptionId,
            resourceGroup,
            selectedTimeRange,
            availableResourceGroups.ToList(),
            functionApps.ToList(),
            ShouldPromptForRefresh: true);
    }

    public void ClearRefreshPrompt()
    {
        if (_snapshot == null)
        {
            return;
        }

        _snapshot = _snapshot with { ShouldPromptForRefresh = false };
    }
}

public sealed record DashboardSnapshot(
    string SubscriptionId,
    string ResourceGroup,
    TimeRange SelectedTimeRange,
    IReadOnlyList<string> AvailableResourceGroups,
    IReadOnlyList<FunctionAppInfo> FunctionApps,
    bool ShouldPromptForRefresh);
