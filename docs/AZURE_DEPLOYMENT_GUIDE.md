# Azure Deployment Guide for Contoso University Dental Clinic

## Prerequisites

1. **Azure CLI**: Install from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli
2. **Azure Subscription**: Active Azure subscription with sufficient permissions
3. **Visual Studio or VS Code**: For publishing (optional - can use Azure CLI)

## Option 1: Automated PowerShell Deployment

### Step 1: Run the Azure Resource Creation Script

```powershell
# Navigate to the project directory
cd "C:\workarea\Contoso University Dental Clinic"

# Run the deployment script (replace parameters with your values)
# Uses F1 Free plan by default
.\scripts\powershell\deploy-to-azure.ps1 `
    -ResourceGroupName "rg-dental-clinic" `
    -AppServicePlanName "asp-dental-clinic" `
    -WebAppName "dental-clinic-app-unique-name" `
    -SqlServerName "sql-dental-clinic-unique-name" `
    -DatabaseName "DentalClinicDb" `
    -EntraAdminUPN "your-admin@yourdomain.com" `
    -Owner "Your Name" `
    -CreatedBy "Your Name" `
    -Location "East US"

# For production use, specify B1 Basic plan instead:
# Add: -AppServiceSku "B1"

# To skip OpenAI creation (if you don't need AI summarization):
# Add: -SkipOpenAI
```

> **Note about Azure OpenAI**: The deployment script will create an Azure OpenAI service for the AI-powered medical history summarization feature. Azure OpenAI is generally available and included with Azure subscriptions. If you don't need this feature:
> 1. Add the `-SkipOpenAI` parameter to skip OpenAI creation
> 2. The application will work without it (AI summaries will use mock data)
> 
> **OpenAI Regions**: Azure OpenAI is only available in specific regions. The script will automatically:
> - Use your preferred location if it supports OpenAI
> - Map your location to the nearest OpenAI-supported region
> - Fall back to East US if no mapping is available

### Step 2: Deploy Application Code

```powershell
# Build and publish the application
dotnet publish -c Release -o ./publish

# Create deployment package
Compress-Archive -Path ./publish/* -DestinationPath dental-clinic-app.zip -Force

# Deploy to Azure Web App
az webapp deployment source config-zip --resource-group "rg-dental-clinic" --name "dental-clinic-app-unique-name" --src dental-clinic-app.zip
```

### Step 3: Run Database Migrations

```powershell
# Run Entity Framework migrations against Azure SQL Database (using Entra ID authentication)
.\scripts\powershell\migrate-azure-database.ps1 `
    -SqlServerName "sql-dental-clinic-unique-name" `
    -DatabaseName "DentalClinicDb"
```

### Step 4: Configure Azure OpenAI Integration

```powershell
# Configure OpenAI model deployment and SQL integration (defaults to GPT-4)
.\scripts\powershell\configure-openai-integration.ps1 `
    -ResourceGroupName "rg-dental-clinic" `
    -SqlServerName "sql-dental-clinic-unique-name" `
    -DatabaseName "DentalClinicDb"
    
# Or explicitly specify GPT-4:
# -OpenAIDeploymentName "gpt-4"
```

This will:
- Create a GPT-4 deployment in your Azure OpenAI service
- Generate a customized SQL script for database integration  
- Provide instructions for completing the SQL-side configuration

## Option 2: Manual Azure Portal Deployment

### Step 1: Create Azure Resources

1. **Create Resource Group**
   - Name: `rg-dental-clinic`
   - Region: `East US`

2. **Create SQL Server and Database**
   - Server name: `sql-dental-clinic-[unique-suffix]`
   - Database name: `DentalClinicDb`
   - Pricing tier: **General Purpose Serverless (Free tier)**
     - 32GB storage included
     - Auto-pauses after 1 hour of inactivity (saves costs)
     - 0.5-2 vCores scaling
     - Perfect for development and light production workloads
   - Authentication: **Entra ID authentication only**
   - Entra ID Admin: Set to your Azure AD user account

3. **Create App Service Plan**
   - Name: `asp-dental-clinic`
   - OS: Windows
   - Pricing tier: F1 Free

4. **Create Web App**
   - Name: `dental-clinic-app-[unique-suffix]`
   - Runtime: .NET 9
   - App Service Plan: Use the one created above

### Step 2: Configure Connection String and Managed Identity

1. **Enable Managed Identity on Web App**
   - Go to Web App → Identity → System assigned → On
   - Copy the Object ID for later use

2. **Add connection string**:
   - Go to Web App → Configuration → Connection strings
   - Add new connection string:
     - Name: `DefaultConnection`
     - Value: `Server=tcp:[your-sql-server].database.windows.net,1433;Initial Catalog=DentalClinicDb;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
     - Type: `SQL Azure`

3. **Grant database access to Web App managed identity**:
   - Connect to your database using Azure Data Studio or SQL Server Management Studio
   - Run: `CREATE USER [your-webapp-name] FROM EXTERNAL PROVIDER; ALTER ROLE db_owner ADD MEMBER [your-webapp-name];`

### Step 3: Deploy Application

**Option A: Using Visual Studio**
1. Right-click project → Publish
2. Choose Azure → Azure App Service (Windows)
3. Select your created Web App
4. Publish

**Option B: Using Azure CLI**
```powershell
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath app.zip -Force
az webapp deployment source config-zip --resource-group "rg-dental-clinic" --name "[your-webapp-name]" --src app.zip
```

### Step 4: Run Database Migrations

```powershell
# Update connection string for Entity Framework
$connectionString = "Server=tcp:[your-sql-server].database.windows.net,1433;Initial Catalog=DentalClinicDb;User ID=dbadmin;Password=[your-password];MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;"

# Run migrations
dotnet ef database update --connection $connectionString
```

## Important Configuration Notes

### Security Considerations

1. **SQL Server Firewall**: The deployment script adds rules for:
   - Azure services (0.0.0.0 - 0.0.0.0)
   - Your current IP address
   - Additional IPs can be added in Azure Portal

2. **App Service Configuration**:
   - HTTPS Only: Enabled by default
   - Authentication: Configure if needed
   - Application Insights: Recommended for monitoring

### Cost Optimization

- **App Service Plan**: F1 Free (FREE - 60 minutes/day compute time)
- **SQL Database**: **General Purpose Serverless (FREE for first 32GB)**
  - Auto-pauses after 1 hour of inactivity (no compute charges when paused)
  - Only pay for actual usage
  - Perfect for development and small production workloads
  - Scales automatically between 0.5-2 vCores based on demand
- **Azure OpenAI**: Pay-per-token usage (approximately $0.03/1K input tokens, $0.06/1K output tokens for GPT-4)
  - Higher cost than GPT-3.5 but significantly better quality and reasoning
  - Only charged when making API calls
  - Consider GPT-3.5-turbo for cost-sensitive scenarios ($0.002/1K tokens)
- **Total estimated cost**: FREE for development and testing (plus OpenAI usage costs)! 🎉
  - Note: F1 plan has 60 minutes/day compute limit - upgrade to B1 Basic (~$55/month) for production use

### Environment Variables

The following are automatically configured:
- `ASPNETCORE_ENVIRONMENT=Production`
- Connection string in Azure configuration

## Testing Deployment

1. Navigate to: `https://[your-webapp-name].azurewebsites.net`
2. Test login with default admin account:
   - Email: `admin@contosodentalclinic.com`
   - Password: (configured in appsettings.json - `DefaultAdmin:Password`)
3. Verify all features work correctly

## Troubleshooting

### Common Issues

1. **Database Connection Errors**
   - Check firewall rules on SQL Server
   - Verify connection string format
   - Ensure correct username/password

2. **Application Startup Errors**
   - Check Application Insights logs
   - Verify all NuGet packages are compatible
   - Check environment-specific configuration

3. **Migration Errors**
   - Ensure Entity Framework tools are installed
   - Verify database permissions
   - Check migration files are included in deployment

4. **OpenAI SQL Integration Errors**
   - **Error**: "Please create a master key in the database or open the master key in the session"
     - **Solution**: Database scoped credentials require a master key. The setup scripts now automatically create one.
     - **Verification**: Run `SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##'` to confirm
   - **Error**: "The database scoped credential cannot be used to invoke an external rest endpoint"
     - **Solution**: The credential name must match your OpenAI service endpoint. Scripts now use the correct format.
     - **Fix**: Run the updated setup script which creates credential as `[https://your-service.openai.azure.com/]`
   - **Error**: "Permission denied" when calling OpenAI
     - **Solution**: Ensure the User-Assigned Managed Identity has "Cognitive Services OpenAI User" role on the OpenAI service
   - **Error**: "Deployment not found"
     - **Solution**: Verify the deployment name matches exactly what was created in Azure OpenAI Studio

### Useful Azure CLI Commands

```powershell
# Check deployment status
az webapp deployment list --resource-group "rg-dental-clinic" --name "[webapp-name]"

# View application logs
az webapp log tail --resource-group "rg-dental-clinic" --name "[webapp-name]"

# Restart web app
az webapp restart --resource-group "rg-dental-clinic" --name "[webapp-name]"
```

## Next Steps After Deployment

1. **Configure Custom Domain** (optional)
2. **Set up SSL Certificate** (Let's Encrypt available)
3. **Configure Application Insights** for monitoring
4. **Set up Azure DevOps** for CI/CD pipeline
5. **Configure backup strategy** for database
6. **Review security settings** and implement additional measures as needed