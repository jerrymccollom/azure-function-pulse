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
    "DemoMode": false,
    "OperationTimeoutSeconds": 300
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
| `Azure:DemoMode` | Enable demo mode with sample data (no Azure auth required) | No | `true` |
| `Azure:OperationTimeoutSeconds` | Timeout in seconds for Azure operations (discovery, metrics, logs) | No | `300` (5 minutes) |
| `HealthThresholds:WarningThreshold` | Failure rate % threshold for warning status (amber) | No | `1.0` |
| `HealthThresholds:CriticalThreshold` | Failure rate % threshold for critical status (red) | No | `5.0` |
| `PathBase` | Path base for reverse-proxy deployments (e.g., Azure Front Door). Must start with `/` and NOT include trailing slash. Leave empty for root deployment. | No | `` (empty) |

### Environment Variables

You can also configure FuncPulse using environment variables:

```bash
export Azure__DemoMode="false"
export Azure__OperationTimeoutSeconds="300"
export PathBase="/funcpulse"  # For reverse-proxy subpath deployments
```

## Authentication

FuncPulse uses **optimized Azure credential selection** based on the runtime environment:

### Local Development
- Uses **AzureCliCredential** 
- Skips the slow managed identity probe (which can timeout after 30+ seconds)
- Requires: `az login` before running

### Azure Hosted (App Service, Container Apps, Functions)
- Uses **DefaultAzureCredential** (full chain)
- Includes Managed Identity authentication
- Automatically detected via environment variables (`WEBSITE_INSTANCE_ID`, `IDENTITY_ENDPOINT`, `MSI_ENDPOINT`)

This optimization significantly improves startup time for local development by avoiding the 30+ second managed identity timeout.

### Authentication Methods

### Authentication Methods

The credential selection is automatic:

**Local Development (AzureCliCredential):**
1. Azure CLI (`az login`)

**Azure Hosted (DefaultAzureCredential):**
1. Environment variables (for service principals)
2. Managed Identity (Azure App Service, Container Apps, Functions, VMs)
3. Azure CLI (fallback)
4. Azure PowerShell (fallback)
5. Visual Studio (fallback)

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

### Deployment Behind Azure Front Door

If deploying behind **Azure Front Door** (AFD), additional configuration is required:

1. **ForwardedHeaders**: FuncPulse includes ForwardedHeaders middleware to read `X-Forwarded-Host` and `X-Forwarded-Proto` from AFD
2. **PathBase**: Configure `PathBase` app setting only if AFD preserves the path prefix
3. **Azure AD Redirect URIs**: Must use the public AFD URL (e.g., `https://ssm-dev.hcahealthcare.cloud/api/function-pulse/signin-oidc`)
4. **Avoiding Doubled Paths**: Do not set both PathBase and AFD path rewriting — choose one pattern

For detailed AFD setup instructions, see **[AZURE_AD_SETUP.md](./AZURE_AD_SETUP.md#step-5a-azure-front-door-configuration-important)**.

**Quick Summary**:
- ForwardedHeaders middleware is **pre-configured** and enabled automatically
- Set `PathBase` app setting if AFD forwards the full path (e.g., `/api/function-pulse`)
- Do NOT set `PathBase` if AFD strips the path prefix
- Azure AD redirect URIs must include the full public path

### Deploying Behind Azure Front Door

FuncPulse supports path-based routing through Azure Front Door or other reverse proxies. Configure the `PathBase` application setting to match your Front Door route:

**Example: Deploying at `/funcpulse` subpath**

1. Configure Azure Front Door route: `/funcpulse/*` → Backend (App Service)
2. Add App Service application setting:
   - **Name**: `PathBase`
   - **Value**: `/funcpulse` (no trailing slash)
3. If using Azure AD authentication, update redirect URIs in your app registration:
   - Redirect URI: `https://<your-domain>/funcpulse/signin-oidc`
   - Logout URL: `https://<your-domain>/funcpulse/signout-callback-oidc`

**Root deployment** (e.g., `https://funcpulse.contoso.com/`): Leave `PathBase` empty or unset.

See [AZURE_AD_SETUP.md](./AZURE_AD_SETUP.md) for complete Azure AD configuration details.

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

1. Authenticate and select your subscription using Azure CLI: `az login` and `az account set --subscription <subscription-id>`
2. Set `"DemoMode": false`
3. Run the application; it discovers resource groups in the selected subscription
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

### Verbose Console Logging

When running with `DemoMode: false`, FuncPulse automatically outputs verbose console logs showing each step of the Azure connection process. This helps diagnose connection stalls or authentication issues.

**Timeouts:** All Azure operations have a configurable timeout (default 300 seconds / 5 minutes). This can be adjusted via `Azure:OperationTimeoutSeconds` in appsettings.json. If authentication or API calls take longer, you'll see a clear timeout error with troubleshooting guidance.

**Example console output:**
```
[15:30:00] FuncPulse starting...
[15:30:00] Configuration loaded:
  - DemoMode: False
[15:30:00] Azure production mode enabled - verbose logging active
[15:30:00] Will use DefaultAzureCredential (tries: Environment → Managed Identity → Azure CLI → PowerShell → Visual Studio)
[15:30:00] Building application...
[15:30:01] FunctionDiscoveryService: Initializing DefaultAzureCredential...
[15:30:01] FunctionDiscoveryService: DefaultAzureCredential created successfully
[15:30:01] MetricsService: Initializing DefaultAzureCredential for Azure Monitor...
[15:30:01] MetricsService: MetricsQueryClient created successfully
[15:30:01] LogsService: Initializing DefaultAzureCredential for Log Analytics...
[15:30:01] LogsService: LogsQueryClient created successfully
[15:30:01] Configuring HTTP pipeline...
[15:30:01] Starting Kestrel HTTP listener...
[15:30:01] Application URLs will be displayed below:
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
```

When a user loads the dashboard, you'll see additional logs:
```
[15:30:15] FunctionDiscoveryService: Starting discovery in subscription 12345678... / RG 'my-functions-rg'
[15:30:15] FunctionDiscoveryService: Resolving subscription resource...
[15:30:15] FunctionDiscoveryService: Fetching resource group 'my-functions-rg'...
[15:30:16] FunctionDiscoveryService: Resource group found, listing web apps...
[15:30:17] FunctionDiscoveryService: Found Function App 'order-processor'
[15:30:17] FunctionDiscoveryService:   - App Insights connected
[15:30:17] FunctionDiscoveryService:   - Listing functions...
[15:30:18] FunctionDiscoveryService:   - Found 3 function(s)
[15:30:18] FunctionDiscoveryService: Discovery complete - 2 web app(s), 2 function app(s)
```

**If authentication stalls**, you'll see where it stops:
```
[15:30:01] FunctionDiscoveryService: Initializing DefaultAzureCredential...
[15:30:15] FunctionDiscoveryService: Starting discovery...
[15:30:15] FunctionDiscoveryService: Fetching resource group 'my-functions-rg'...
<hangs here - credential chain is trying each method, will timeout after 60 seconds>
[15:31:15] FunctionDiscoveryService: TIMEOUT after 60 seconds
```

After timeout, you'll see a helpful error message in the UI explaining common causes:
- Azure CLI authentication is slow or stale
- Network connectivity issues
- Insufficient permissions

**Solution:** Try `az login --use-device-code` for more reliable authentication.

### Common Issues

### "Blank screen on first load" / Application Hangs

**Cause**: This was caused by a Blazor Server lifecycle issue where `OnInitializedAsync()` was blocking the first render while authenticating to Azure.

**Fixed**: Data loading now happens in `OnAfterRenderAsync(firstRender)` so the loading overlay renders immediately, then Azure authentication begins. Combined with the 60-second timeout, users now see clear feedback instead of a blank screen.

If you still experience delays, it's the authentication process (see verbose logging output). Use `az login --use-device-code` for faster, more reliable authentication.

### "Unable to determine the Azure subscription"

**Cause**: The Azure CLI is not logged in or has no selected subscription.

**Solution**: Run `az login`, then select a subscription with `az account set --subscription <subscription-id>`. Or set `"DemoMode": true` to use sample data.

### "Failed to discover function apps"

**Cause**: Authentication failed or insufficient permissions.

**Solution**:
1. Verify authentication: `az account show`
2. Verify RBAC permissions: Ensure you have **Reader** role on the resource group
3. Confirm the desired subscription is selected with `az account show`; resource groups are discovered from it.

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

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For issues or questions, please open an issue in the repository.
