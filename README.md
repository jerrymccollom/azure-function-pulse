# FuncPulse

**FuncPulse** is an Azure Functions resource-group dashboard built with .NET 10 and Blazor (interactive server). It provides a centralized view of all Azure Functions within a resource group, displaying health metrics, invocation statistics, and detailed failure logs.

## Features

- **Automatic Discovery**: Discovers all Azure Function Apps and their functions within a configured resource group
- **Time-Range Analytics**: View metrics across configurable time windows (24 hours, 7 days, 30 days)
- **Health Monitoring**: Visual health indicators with green/amber/red status based on failure rates
- **Success/Failure Metrics**: Real-time tracking of function invocations and success rates
- **Failure Drill-Down**: Click through to view detailed logs for failed invocations via Application Insights
- **Demo Mode**: Run locally without Azure credentials using sample data

## Architecture

```
FuncPulse/
├── src/
│   ├── FuncPulse.Web/          # Blazor Web App (Interactive Server)
│   └── FuncPulse.Core/         # Domain models and Azure services
├── tests/
│   └── FuncPulse.Tests/        # Unit tests
└── FuncPulse.sln
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.401 or later)
- Azure subscription with Function Apps (for production use)
- Appropriate Azure RBAC permissions (see below)

## Configuration

### Application Settings

FuncPulse is configured via `appsettings.json` or environment variables:

```json
{
  "Azure": {
    "SubscriptionId": "your-subscription-id",
    "ResourceGroup": "your-resource-group",
    "TenantId": "your-tenant-id",
    "DemoMode": false
  },
  "HealthThresholds": {
    "WarningThreshold": 1.0,
    "CriticalThreshold": 5.0
  }
}
```

#### Configuration Options

| Setting | Description | Required | Default |
|---------|-------------|----------|---------|
| `Azure:SubscriptionId` | Azure subscription ID containing the resource group | Yes (unless DemoMode) | - |
| `Azure:ResourceGroup` | Resource group name to monitor | Yes (unless DemoMode) | - |
| `Azure:TenantId` | Azure AD tenant ID | No | - |
| `Azure:DemoMode` | Enable demo mode with sample data (no Azure auth required) | No | `true` |
| `HealthThresholds:WarningThreshold` | Failure rate % threshold for warning status (amber) | No | `1.0` |
| `HealthThresholds:CriticalThreshold` | Failure rate % threshold for critical status (red) | No | `5.0` |

### Environment Variables

You can also configure FuncPulse using environment variables:

```bash
export Azure__SubscriptionId="your-subscription-id"
export Azure__ResourceGroup="your-resource-group"
export Azure__TenantId="your-tenant-id"
export Azure__DemoMode="false"
```

## Authentication

FuncPulse uses **DefaultAzureCredential** from the Azure Identity library, which automatically attempts authentication in the following order:

1. **Environment variables** (for service principals)
2. **Managed Identity** (when deployed to Azure)
3. **Visual Studio** (when running locally)
4. **Azure CLI** (when running locally)
5. **Azure PowerShell** (when running locally)

### Local Development

For local development, authenticate using the Azure CLI:

```bash
az login
az account set --subscription <subscription-id>
```

Or authenticate with Visual Studio or Azure PowerShell.

### Production Deployment

For production deployments to Azure App Service or Container Apps:

1. Enable **System-assigned Managed Identity** on your hosting resource
2. Grant the managed identity the required RBAC permissions (see below)
3. Set `Azure:DemoMode` to `false` in application configuration

## Required Azure RBAC Permissions

The identity running FuncPulse (your user account, service principal, or managed identity) requires the following permissions:

### Minimum Permissions (Scope: Resource Group)

| Role | Purpose | Scope |
|------|---------|-------|
| **Reader** | Discover Function Apps and list functions | Resource Group |
| **Monitoring Reader** | Query Azure Monitor Metrics for invocation counts | Resource Group |

### Additional Permissions for Log Viewing

If you want to view failed invocation logs via Application Insights:

| Role | Purpose | Scope |
|------|---------|-------|
| **Log Analytics Reader** | Query Application Insights logs | Application Insights resource or Log Analytics workspace |

### Assigning Permissions

Using Azure CLI:

```bash
# Get your identity (current user)
USER_PRINCIPAL=$(az ad signed-in-user show --query id -o tsv)

# Or use managed identity object ID
# MANAGED_IDENTITY_OBJECT_ID="..."

# Assign Reader role
az role assignment create \
  --assignee $USER_PRINCIPAL \
  --role "Reader" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group>"

# Assign Monitoring Reader role
az role assignment create \
  --assignee $USER_PRINCIPAL \
  --role "Monitoring Reader" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group>"

# (Optional) Assign Log Analytics Reader for Application Insights
az role assignment create \
  --assignee $USER_PRINCIPAL \
  --role "Log Analytics Reader" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Insights/components/<app-insights-name>"
```

## Running Locally

### Demo Mode (No Azure Authentication)

To quickly test the UI with sample data:

```bash
cd src/FuncPulse.Web
dotnet run
```

Navigate to `https://localhost:5001` (or the URL shown in the console).

Demo mode is enabled by default in `appsettings.json` (`"DemoMode": true`).

### Production Mode (Azure Authentication)

1. Configure your Azure subscription and resource group in `appsettings.json` or via environment variables
2. Set `"DemoMode": false`
3. Authenticate using Azure CLI: `az login`
4. Run the application:

```bash
cd src/FuncPulse.Web
dotnet run
```

Navigate to `https://localhost:5001`.

## Building the Solution

```bash
# Build all projects
dotnet build

# Run tests
dotnet test

# Publish for deployment
dotnet publish src/FuncPulse.Web -c Release -o ./publish
```

## Application Insights Integration

FuncPulse automatically discovers Application Insights configuration for each Function App by reading the `APPLICATIONINSIGHTS_CONNECTION_STRING` setting.

**If Application Insights is not configured:**
- Metrics (invocation counts, success/failure rates) are still shown via Azure Monitor
- Log viewing for failed invocations will display a graceful "Logs unavailable" message

**To enable full log viewing:**
1. Ensure your Function Apps have Application Insights enabled
2. Grant your identity **Log Analytics Reader** role on the Application Insights resources
3. FuncPulse will automatically detect and query logs for failed invocations

## Health Status Logic

Function health is calculated based on failure rates within the selected time range:

| Status | Badge Color | Condition |
|--------|-------------|-----------|
| **Healthy** | Green | Failure rate < 1% (configurable) |
| **Warning** | Amber | Failure rate 1% - 5% (configurable) |
| **Critical** | Red | Failure rate > 5% (configurable) |
| **Unknown** | Gray | No invocations in the time range |

Thresholds can be customized in `appsettings.json` under `HealthThresholds`.

## Technology Stack

- **.NET 10.0**: Latest .NET runtime
- **Blazor Server**: Interactive UI with server-side rendering
- **Azure SDK for .NET**:
  - `Azure.Identity` - Authentication
  - `Azure.ResourceManager` - Resource discovery
  - `Azure.ResourceManager.AppService` - Function App management
  - `Azure.Monitor.Query` - Metrics and logs querying
- **Bootstrap 5**: Responsive UI styling

## Troubleshooting

### "Azure subscription and resource group not configured"

**Cause**: `Azure:SubscriptionId` or `Azure:ResourceGroup` is not set, and `DemoMode` is `false`.

**Solution**: Either:
- Set the configuration values in `appsettings.json` or via environment variables
- Or set `"DemoMode": true` to use sample data

### "Failed to discover function apps"

**Cause**: Authentication failed or insufficient permissions.

**Solution**:
1. Verify authentication: `az account show`
2. Verify RBAC permissions: Ensure you have **Reader** role on the resource group
3. Check the resource group name and subscription ID are correct

### "Logs unavailable - Application Insights not configured"

**Cause**: The Function App does not have Application Insights linked, or you lack permission to query logs.

**Solution**:
1. Enable Application Insights for your Function Apps
2. Grant your identity **Log Analytics Reader** role on the Application Insights resource
3. Ensure the `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting is configured

### Authentication errors in production

**Cause**: Managed identity not enabled or lacks permissions.

**Solution**:
1. Enable system-assigned managed identity on your hosting resource (App Service, Container App, etc.)
2. Assign the required RBAC roles to the managed identity (see "Required Azure RBAC Permissions")

## Contributing

This is a purpose-built dashboard for monitoring Azure Functions. Contributions and feedback are welcome.

## License

This project is provided as-is for internal use.

## Support

For issues or questions, please open an issue in the repository.
