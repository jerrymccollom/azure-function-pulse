# Azure AD Authentication Setup for FuncPulse

This document describes how to configure Azure Active Directory (Azure AD) authentication for FuncPulse when deployed to Azure App Service. This allows users browsing the FuncPulse dashboard to sign in with their Microsoft/Azure AD account, and FuncPulse will use their credentials to access Azure resources (Function Apps, Application Insights, etc.).

## Overview

FuncPulse supports three authentication modes:

1. **Local Development**: Uses Azure CLI credentials (`az login`) - no additional setup needed
2. **Demo Mode**: Uses sample data - no authentication required
3. **Azure Hosted with User Authentication**: Uses signed-in user's Azure AD token (this guide)

## Prerequisites

- An Azure subscription where you will deploy FuncPulse
- Permission to create Azure AD App Registrations
- FuncPulse deployed to Azure App Service

## Step 1: Create an Azure AD App Registration

1. Go to the [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure the registration:
   - **Name**: `FuncPulse-AppService` (or your preferred name)
   - **Supported account types**: 
     - Choose "Accounts in this organizational directory only" for single-tenant
     - Or "Accounts in any organizational directory" for multi-tenant
   - **Redirect URI**: 
     - Platform: **Web**
     - URI: `https://<your-app-service-name>.azurewebsites.net/signin-oidc`
     - Example: `https://funcpulse-prod.azurewebsites.net/signin-oidc`
5. Click **Register**

## Step 2: Configure API Permissions

The app needs permission to access Azure Resource Manager and Application Insights on behalf of the signed-in user.

1. In your app registration, go to **API permissions**
2. Click **Add a permission**
3. Select **Azure Service Management**
4. Check **user_impersonation** (Access Azure Service Management as organization users)
5. Click **Add permissions**
6. Click **Grant admin consent** (requires admin privileges)

### Additional Permissions (if needed)

If you need access to Application Insights data or Log Analytics:

1. Click **Add a permission** > **APIs my organization uses**
2. Search for and select **Azure Monitor**
3. Check **user_impersonation**
4. Click **Add permissions**
5. Grant admin consent

## Step 3: Create a Client Secret

1. In your app registration, go to **Certificates & secrets**
2. Click **New client secret**
3. Add a description (e.g., "FuncPulse Production")
4. Select an expiration period (e.g., 24 months)
5. Click **Add**
6. **Important**: Copy the secret **Value** immediately - you won't be able to see it again

## Step 4: Configure App Service Settings

Add the following application settings to your Azure App Service:

### Method A: Azure Portal

1. Go to your App Service in the Azure Portal
2. Navigate to **Configuration** > **Application settings**
3. Add the following settings:

| Name | Value | Example |
|------|-------|---------|
| `AzureAd__TenantId` | Your Azure AD tenant ID | `12345678-1234-1234-1234-123456789abc` |
| `AzureAd__ClientId` | Your app registration client ID | `87654321-4321-4321-4321-cba987654321` |
| `AzureAd__ClientSecret` | Your client secret value | `abc~123...` |
| `AzureAd__Domain` | Your Azure AD domain | `contoso.onmicrosoft.com` |
| `AzureAd__Instance` | Azure AD instance | `https://login.microsoftonline.com/` |
| `Azure__DemoMode` | Set to false to enable Azure mode | `false` |

4. Click **Save**

### Method B: Azure CLI

```bash
az webapp config appsettings set \
  --name <your-app-service-name> \
  --resource-group <your-resource-group> \
  --settings \
    AzureAd__TenantId="<your-tenant-id>" \
    AzureAd__ClientId="<your-client-id>" \
    AzureAd__ClientSecret="<your-client-secret>" \
    AzureAd__Domain="<your-domain>.onmicrosoft.com" \
    AzureAd__Instance="https://login.microsoftonline.com/" \
    Azure__DemoMode="false"
```

## Step 5: Configure Redirect URIs

Make sure your App Registration has the correct redirect URIs configured:

1. Go to your app registration > **Authentication**
2. Under **Platform configurations** > **Web**, verify:
   - Redirect URI: `https://<your-app-service-name>.azurewebsites.net/signin-oidc`
   - Logout URL: `https://<your-app-service-name>.azurewebsites.net/signout-callback-oidc`
3. Under **Implicit grant and hybrid flows**, ensure:
   - ✅ ID tokens (used for implicit and hybrid flows)
4. Click **Save**

## Step 6: Assign User Permissions

Users need appropriate Azure RBAC permissions to view Function Apps and Application Insights data.

### Required Azure Roles

Assign users one or more of these roles on the subscription or resource group:

- **Reader**: View Function Apps and resources
- **Monitoring Reader**: View Application Insights metrics and logs
- **Website Contributor**: Manage Function Apps (start, stop, restart)

### Assign Roles (Azure Portal)

1. Go to your subscription or resource group
2. Navigate to **Access control (IAM)**
3. Click **Add** > **Add role assignment**
4. Select the role (e.g., **Reader**)
5. Click **Next**
6. Click **Select members** and find the users
7. Click **Select** > **Review + assign**

## Step 7: Test the Configuration

1. Navigate to your FuncPulse App Service URL
2. You should see a **Sign in with Microsoft** button
3. Click the button and sign in with your Azure AD account
4. After successful sign-in, you should see your email in the top bar
5. The dashboard should load Function Apps using your credentials

## Troubleshooting

### "Login failed" or "Permission denied"

- Verify the user has appropriate Azure RBAC roles (Reader, Monitoring Reader)
- Check that admin consent was granted for API permissions
- Ensure the subscription ID and resource group are correct

### "Application not found" error during login

- Verify the TenantId, ClientId, and Domain are correct
- Check that the redirect URI exactly matches the app registration
- Ensure the App Service application settings are saved and restarted

### Token acquisition fails

- Verify the client secret is correct and not expired
- Check that the AzureAd__ClientSecret setting is properly configured
- Review App Service logs for detailed error messages

### Users see CLI/Managed Identity instead of their token

- Verify the AzureAd configuration section exists in App Service settings
- Check that all required settings (TenantId, ClientId, ClientSecret, Domain) are present
- Ensure Azure__DemoMode is set to `false`
- Review application logs for authentication middleware initialization

## Security Best Practices

1. **Use Key Vault for Secrets**: Instead of storing the client secret in App Service settings directly, use Azure Key Vault:
   ```bash
   az keyvault secret set --vault-name <your-keyvault> --name "AzureAd-ClientSecret" --value "<your-secret>"
   az webapp config appsettings set --name <your-app> --resource-group <your-rg> \
     --settings AzureAd__ClientSecret="@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/AzureAd-ClientSecret/)"
   ```

2. **Enable Managed Identity**: Assign a system-assigned managed identity to your App Service to access Key Vault

3. **Restrict Access**: Configure Conditional Access policies in Azure AD to restrict who can access FuncPulse

4. **Monitor Access**: Review sign-in logs in Azure AD to monitor access to the application

5. **Rotate Secrets**: Regularly rotate client secrets and update the configuration

## Local Development

Local development continues to use Azure CLI credentials. The Azure AD authentication only activates when:
1. Running in Azure (detected via environment variables)
2. Not in Demo Mode
3. AzureAd configuration section exists

To test locally without Azure AD:
- Set `Azure__DemoMode` to `true` in `appsettings.Development.json`
- Or use `az login` to authenticate with your Azure account

## Architecture Notes

- **User-Delegated Access**: When a user signs in, FuncPulse obtains an access token for Azure Management API on their behalf
- **Per-User Permissions**: Each user sees only the resources they have access to in Azure
- **Token Caching**: Tokens are cached in-memory per user session
- **Fallback**: If token acquisition fails, the app falls back to default credentials (Managed Identity or CLI)
