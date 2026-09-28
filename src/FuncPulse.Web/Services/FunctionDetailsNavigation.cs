using FuncPulse.Core.Models;

namespace FuncPulse.Web.Services;

public static class FunctionDetailsNavigation
{
    public const string AppInsightsResourceIdQueryParameter = "appInsightsResourceId";
    public const string AppResourceIdQueryParameter = "appResourceId";

    public static string BuildUri(FunctionAppInfo app, FunctionInfo function)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(function);

        var path = $"/function/{Uri.EscapeDataString(app.Name)}/{Uri.EscapeDataString(function.Name)}";

        var queryParams = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(app.AppInsightsResourceId))
        {
            queryParams.Add($"{AppInsightsResourceIdQueryParameter}={Uri.EscapeDataString(app.AppInsightsResourceId)}");
        }
        
        if (!string.IsNullOrWhiteSpace(app.ResourceId))
        {
            queryParams.Add($"{AppResourceIdQueryParameter}={Uri.EscapeDataString(app.ResourceId)}");
        }

        return queryParams.Count > 0 
            ? $"{path}?{string.Join("&", queryParams)}"
            : path;
    }
}
