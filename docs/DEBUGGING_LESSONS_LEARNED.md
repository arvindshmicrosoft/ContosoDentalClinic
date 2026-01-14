# Debugging and Troubleshooting Lessons Learned

This document consolidates debugging experiences and solutions discovered during the development of the Contoso University Dental Clinic application, particularly for Azure deployments.

---

## Table of Contents
1. [HTTP Error 500.30 - ASP.NET Core App Failed to Start](#http-error-50030---aspnet-core-app-failed-to-start)
2. [Managed Identity Authentication Issues](#managed-identity-authentication-issues)
3. [Database Connection Management](#database-connection-management)
4. [Azure OpenAI Integration Issues](#azure-openai-integration-issues)

---

## HTTP Error 500.30 - ASP.NET Core App Failed to Start

### Symptoms
- Application shows "HTTP Error 500.30" in the browser
- Azure Web App fails to start
- No detailed error message visible

### Quick Diagnosis Steps

```powershell
# Enable detailed error logging
az webapp log config --name <your-webapp-name> --resource-group <your-rg> \
    --application-logging filesystem --level verbose

# Stream live logs
az webapp log tail --name <your-webapp-name> --resource-group <your-rg>
```

### Common Causes and Solutions

#### 1. Database Connection Issues (Most Common)
**Problem**: Managed identity doesn't have database permissions.

**Solution**: Connect to Azure SQL Database with your Entra ID admin account and run:
```sql
-- Create the managed identity user (replace with your identity name)
CREATE USER [<your-webapp-identity-name>] FROM EXTERNAL PROVIDER;

-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER [<your-webapp-identity-name>];
ALTER ROLE db_datawriter ADD MEMBER [<your-webapp-identity-name>];
ALTER ROLE db_ddladmin ADD MEMBER [<your-webapp-identity-name>];

-- Verify the user was created
SELECT name, type_desc, authentication_type_desc 
FROM sys.database_principals 
WHERE name = '<your-webapp-identity-name>';
```

#### 2. Missing Environment Variables
**Problem**: AZURE_CLIENT_ID not set for user-assigned managed identity.

**Solution**:
```powershell
az webapp config appsettings set --name <your-webapp-name> \
    --resource-group <your-rg> \
    --settings ASPNETCORE_ENVIRONMENT=Production \
               AZURE_CLIENT_ID=<your-managed-identity-client-id>
```

#### 3. Connection String Format Issues
**Problem**: Connection string missing User Id parameter for user-assigned managed identity.

**Solution**: Use this connection string format:
```
Server=tcp:<your-server>.database.windows.net,1433;
Initial Catalog=<your-database>;
Authentication=Active Directory Managed Identity;
User Id=<your-managed-identity-client-id>;
Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

#### 4. Runtime/SDK Version Mismatch
**Problem**: Azure App Service doesn't have the correct .NET version.

**Solution**:
```powershell
# Check current .NET version
az webapp config show --name <your-webapp-name> \
    --resource-group <your-rg> --query "netFrameworkVersion"
```

---

## Managed Identity Authentication Issues

### Error Message
```
ManagedIdentityCredential authentication failed: Service request failed.
Status: 400 (Bad Request)
```

### Root Causes

1. **Missing AZURE_CLIENT_ID**: Required for user-assigned managed identities
2. **Permission not propagated**: Azure AD permissions can take up to 10 minutes
3. **Wrong identity type**: System-assigned vs user-assigned confusion

### Solution Checklist

1. ✅ Verify managed identity is enabled on the Web App
2. ✅ Get the managed identity's Client ID
3. ✅ Set AZURE_CLIENT_ID in App Settings
4. ✅ Add managed identity as database user
5. ✅ Grant appropriate database roles
6. ✅ Wait 5-10 minutes for permissions to propagate
7. ✅ Restart the Web App

---

## Database Connection Management

### Issue: Connection Pool Exhaustion

**Symptoms**:
- Intermittent "timeout expired" errors
- Application becomes unresponsive under load
- Connection pool exhausted warnings in logs

### Root Causes Found

1. **Manual SqlConnection Creation**: Bypasses Entity Framework's connection pooling
2. **Connections Not Closed**: Missing `finally` blocks for cleanup
3. **Long-Running Operations**: Holding connections during Azure OpenAI calls

### Solutions Implemented

#### 1. Use Entity Framework's Connection Management
```csharp
// ❌ BEFORE (Problematic)
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
using var command = new SqlCommand("...", connection);

// ✅ AFTER (Recommended)
var result = await _context.Database.SqlQueryRaw<int?>(
    "SELECT OBJECT_ID('...')").FirstOrDefaultAsync();
```

#### 2. Proper Connection Lifecycle Management
```csharp
// For raw SQL operations that need manual connection
await _context.Database.OpenConnectionAsync();
try
{
    using var reader = await command.ExecuteReaderAsync();
    // Process results...
}
finally
{
    await _context.Database.CloseConnectionAsync();  // Always cleanup
}
```

#### 3. Configure Command Timeouts
```csharp
options.UseSqlServer(connectionString, sqlOptions =>
{
    sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorNumbersToAdd: null);
        
    sqlOptions.CommandTimeout(30);  // Development
    
    if (builder.Environment.IsProduction())
    {
        sqlOptions.CommandTimeout(60);  // Production (Azure latency)
    }
});
```

#### 4. Enable Connection Monitoring
```csharp
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Information);
```

---

## Azure OpenAI Integration Issues

### Issue: "Database scoped credential cannot be used"

**Symptoms**:
- sp_invoke_external_rest_endpoint fails
- Error about credential not matching endpoint

### Solution

The credential name must **exactly match** the base URL of your OpenAI service:

```sql
-- ❌ WRONG (generic cognitive services)
CREATE DATABASE SCOPED CREDENTIAL [https://cognitiveservices.azure.com/]

-- ✅ CORRECT (specific to your OpenAI service)
CREATE DATABASE SCOPED CREDENTIAL [https://<your-service-name>.openai.azure.com/]
WITH IDENTITY = 'Managed Identity',
     SECRET = '{"resourceid":"https://cognitiveservices.azure.com/"}';
```

### Prerequisites Checklist

1. ✅ Database Master Key exists
2. ✅ Credential name matches OpenAI service endpoint
3. ✅ Managed identity has "Cognitive Services OpenAI User" role
4. ✅ OpenAI deployment exists and is accessible

### Validation Commands

```sql
-- Check for Azure SQL Database features
SELECT OBJECT_ID('sp_invoke_external_rest_endpoint') AS AzureSQLFeature;

-- Verify master key
SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##';

-- Check credentials
SELECT name, credential_identity, create_date 
FROM sys.database_scoped_credentials;
```

---

## General Debugging Tips

### Azure CLI Commands for Troubleshooting

```powershell
# View application logs
az webapp log tail --name <your-webapp-name> --resource-group <your-rg>

# Check app settings
az webapp config appsettings list --name <your-webapp-name> --resource-group <your-rg>

# Check connection strings
az webapp config connection-string list --name <your-webapp-name> --resource-group <your-rg>

# Restart the web app
az webapp restart --name <your-webapp-name> --resource-group <your-rg>

# View deployment history
az webapp deployment list-publishing-profiles --name <your-webapp-name> --resource-group <your-rg>
```

### Entity Framework Migrations

```powershell
# Run migrations against Azure SQL Database
dotnet ef database update --connection "<your-azure-connection-string>"
```

### Manual Connection Test

```powershell
# Test SQL Server connection
sqlcmd -S <your-server>.database.windows.net -d <your-database> -G -l 30
```

---

## Emergency Recovery Procedures

### If Application Completely Fails

1. **Check Azure Portal** for any service outages
2. **Enable detailed logging** (see commands above)
3. **Review recent deployments** for code changes
4. **Test database connectivity** independently
5. **Restart the Web App** (sometimes cures transient issues)
6. **Redeploy** if all else fails

### If Database Permissions Lost

```sql
-- Re-grant all permissions
ALTER ROLE db_datareader ADD MEMBER [<your-identity>];
ALTER ROLE db_datawriter ADD MEMBER [<your-identity>];
ALTER ROLE db_ddladmin ADD MEMBER [<your-identity>];
```

---

## Lessons Learned Summary

| Issue | Root Cause | Prevention |
|-------|-----------|------------|
| 500.30 startup failure | Missing DB permissions | Document and script permission setup |
| Connection pool exhaustion | Manual SqlConnection usage | Always use Entity Framework's connection management |
| Managed identity auth failure | Missing AZURE_CLIENT_ID | Include in deployment checklist |
| OpenAI credential errors | Mismatched credential name | Validate credential matches endpoint exactly |
| Timeout errors | No command timeout configured | Set explicit timeouts in DbContext configuration |

---

*This document should be updated as new debugging scenarios are encountered.*
