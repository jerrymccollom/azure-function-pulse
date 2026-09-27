using FuncPulse.Core.Models;

namespace FuncPulse.Web.Services;

public static class FunctionDetailsNavigation
{
    public const string AppInsightsResourceIdQueryParameter = "appInsightsResourceId";

    public static string BuildUri(FunctionAppInfo app, FunctionInfo function)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(function);

        var path = $"/function/{Uri.EscapeDataString(app.Name)}/{Uri.EscapeDataString(function.Name)}";

        return string.IsNullOrWhiteSpace(app.AppInsightsResourceId)
            ? path
            : $"{path}?{AppInsightsResourceIdQueryParameter}={Uri.EscapeDataString(app.AppInsightsResourceId)}";
    }
}
