# Azure Deployment Script for Contoso University Dental Clinic
# Run this script from PowerShell with Azure CLI installed and authenticated
# 
# This script uses F1 Free App Service Plan (60 minutes/day compute limit)
# For production use, consider upgrading to B1 Basic plan by changing --sku F1 to --sku B1

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppServicePlanName,
    
    [Parameter(Mandatory=$true)]
    [string]$WebAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$SqlServerName,
    
    [Parameter(Mandatory=$true)]
    [string]$DatabaseName,
    
    [Parameter(Mandatory=$true)]
    [string]$EntraAdminUPN,
    
    [Parameter(Mandatory=$true)]
    [string]$Owner,
    
    [Parameter(Mandatory=$true)]
    [string]$CreatedBy,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("F1", "B1", "B2", "B3", "S1", "S2", "S3")]
    [string]$AppServiceSku = "F1",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipOpenAI
)

Write-Host "Starting Azure deployment for Contoso University Dental Clinic..." -ForegroundColor Green

# Function to find the best available Azure OpenAI region
function Get-AvailableOpenAILocation {
    param([string]$PreferredLocation)
    
    # Known Azure OpenAI regions (as of October 2025) in order of preference
    # These are regions where OpenAI services are generally available
    $OpenAIRegions = @(
        @{ Name = "East US"; Code = "eastus" },
        @{ Name = "East US 2"; Code = "eastus2" },
        @{ Name = "West US"; Code = "westus" },
        @{ Name = "West US 2"; Code = "westus2" },
        @{ Name = "South Central US"; Code = "southcentralus" },
        @{ Name = "West Europe"; Code = "westeurope" },
        @{ Name = "North Europe"; Code = "northeurope" },
        @{ Name = "UK South"; Code = "uksouth" },
        @{ Name = "France Central"; Code = "francecentral" },
        @{ Name = "Switzerland North"; Code = "switzerlandnorth" },
        @{ Name = "Australia East"; Code = "australiaeast" },
        @{ Name = "Japan East"; Code = "japaneast" },
        @{ Name = "Canada East"; Code = "canadaeast" }
    )
    
    # Convert preferred location to match our region list
    $PreferredMatch = $OpenAIRegions | Where-Object { $_.Name -eq $PreferredLocation }
    
    if ($PreferredMatch) {
        Write-Host "Preferred location '$PreferredLocation' supports OpenAI" -ForegroundColor Green
        return $PreferredMatch.Name
    }
    
    # Find closest region based on common mappings
    $RegionMapping = @{
        "Central US" = "South Central US"
        "North Central US" = "East US 2"
        "West Central US" = "West US 2"
        "South Central US" = "South Central US"
        "East Asia" = "Japan East"
        "Southeast Asia" = "Australia East"
        "West India" = "West Europe"
        "Central India" = "West Europe"
        "South India" = "Australia East"
        "Brazil South" = "East US"
    }
    
    if ($RegionMapping.ContainsKey($PreferredLocation)) {
        $MappedRegion = $RegionMapping[$PreferredLocation]
        Write-Host "Mapping '$PreferredLocation' to OpenAI-supported region: $MappedRegion" -ForegroundColor Yellow
        return $MappedRegion
    }
    
    # Default fallback - try West US
    Write-Host "Using default OpenAI region: West US (preferred location '$PreferredLocation' not supported)" -ForegroundColor Yellow
    return "West US"
}

try {
    # Check if already logged in to Azure
    Write-Host "Checking Azure login status..." -ForegroundColor Yellow
    $account = az account show 2>$null
    if (-not $account) {
        Write-Host "Please login to Azure..." -ForegroundColor Yellow
        az login
    }

    # Get current user's object ID for Entra ID authentication
    Write-Host "Getting current user information for Entra ID setup..." -ForegroundColor Yellow
    $currentUser = az ad signed-in-user show | ConvertFrom-Json
    $currentUserObjectId = $currentUser.id

    # Create Resource Group with tags
    Write-Host "Creating Resource Group: $ResourceGroupName with tags" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location --tags Owner="$Owner" CreatedBy="$CreatedBy" Project="Contoso University Dental Clinic" Environment="Production"

    # Create App Service Plan (Free F1 for cost optimization)
    Write-Host "Creating App Service Plan: $AppServicePlanName ($AppServiceSku tier)" -ForegroundColor Yellow
    az appservice plan create --name $AppServicePlanName --resource-group $ResourceGroupName --sku $AppServiceSku

    # Create Web App
    Write-Host "Creating Web App: $WebAppName" -ForegroundColor Yellow
    az webapp create --resource-group $ResourceGroupName --plan $AppServicePlanName --name $WebAppName --runtime "dotnet:8"

    # Create User-Assigned Managed Identity
    $ManagedIdentityName = "$WebAppName-identity"
    Write-Host "Creating User-Assigned Managed Identity: $ManagedIdentityName" -ForegroundColor Yellow
    $identityResult = az identity create --name $ManagedIdentityName --resource-group $ResourceGroupName --location $Location | ConvertFrom-Json
    $identityId = $identityResult.id
    $identityClientId = $identityResult.clientId
    $identityPrincipalId = $identityResult.principalId

    # Assign the managed identity to the Web App
    Write-Host "Assigning managed identity to Web App..." -ForegroundColor Yellow
    az webapp identity assign --resource-group $ResourceGroupName --name $WebAppName --identities $identityId

    # Create SQL Server with Entra ID authentication only
    Write-Host "Creating SQL Server: $SqlServerName (Entra ID authentication only)" -ForegroundColor Yellow
    az sql server create --name $SqlServerName --resource-group $ResourceGroupName --location $Location --enable-ad-only-auth --external-admin-principal-type User --external-admin-name $EntraAdminUPN --external-admin-sid $currentUserObjectId --assign-identity --identity-type UserAssigned --user-assigned-identity-id $identityId --primary-user-assigned-identity-id $identityId

    # Create SQL Database (using Free tier - 32GB storage, serverless compute)
    Write-Host "Creating SQL Database: $DatabaseName (Free tier)" -ForegroundColor Yellow
    az sql db create -g $ResourceGroupName -s $SqlServerName -n $DatabaseName -e GeneralPurpose -f Gen5 -c 2 --compute-model Serverless --use-free-limit --free-limit-exhaustion-behavior AutoPause

    # Create Azure OpenAI resource (optional, requires special approval)
    $OpenAIServiceName = "$ResourceGroupName-openai-svc"
    
    if ($SkipOpenAI) {
        Write-Host "⏭️  Skipping Azure OpenAI Service creation (SkipOpenAI parameter specified)" -ForegroundColor Yellow
        $OpenAICreated = $false
        $OpenAIEndpoint = "NOT_CREATED"
        $OpenAIServiceName = "NOT_CREATED"
    } else {
        Write-Host "Attempting to create Azure OpenAI Service: $OpenAIServiceName" -ForegroundColor Yellow
        Write-Host "Note: Azure OpenAI requires special subscription approval" -ForegroundColor Cyan
        
        # Find the best available region for OpenAI
        $OpenAILocation = Get-AvailableOpenAILocation -PreferredLocation $Location
        Write-Host "Selected OpenAI region: $OpenAILocation" -ForegroundColor Cyan
    
    try {
        az cognitiveservices account create --name $OpenAIServiceName --resource-group $ResourceGroupName --location $OpenAILocation --kind OpenAI --sku S0 --custom-domain $OpenAIServiceName 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Azure OpenAI Service created successfully!" -ForegroundColor Green
            
            # Assign "Cognitive Services OpenAI User" role to the managed identity at resource group level
            Write-Host "Assigning Cognitive Services OpenAI User role to managed identity..." -ForegroundColor Yellow
            az role assignment create --assignee $identityPrincipalId --role "Cognitive Services OpenAI User" --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroupName"
            
            $OpenAICreated = $true
            $OpenAIEndpoint = "https://$OpenAIServiceName.openai.azure.com/"
        } else {
            throw "Failed to create Azure OpenAI service"
        }
    }
    catch {
        Write-Host "⚠️  Azure OpenAI Service creation failed" -ForegroundColor Yellow
        Write-Host "This is expected if your subscription doesn't have Azure OpenAI access" -ForegroundColor Cyan
        Write-Host "You can request access at: https://aka.ms/oai/access" -ForegroundColor Cyan
        Write-Host "Continuing deployment without OpenAI integration..." -ForegroundColor Yellow
        
        $OpenAICreated = $false
        $OpenAIEndpoint = "NOT_CREATED"
        $OpenAIServiceName = "NOT_CREATED"
    }
    }

    # Configure SQL Server firewall to allow Azure services
    Write-Host "Configuring SQL Server firewall rules..." -ForegroundColor Yellow
    az sql server firewall-rule create --resource-group $ResourceGroupName --server $SqlServerName --name "AllowAzureServices" --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

    # Get your current IP and add firewall rule for local access
    $MyIP = (Invoke-WebRequest -uri "https://api.ipify.org/").Content.Trim()
    Write-Host "Adding firewall rule for your IP: $MyIP" -ForegroundColor Yellow
    az sql server firewall-rule create --resource-group $ResourceGroupName --server $SqlServerName --name "MyLocalIP" --start-ip-address $MyIP --end-ip-address $MyIP

    # Wait for managed identity propagation
    Write-Host "Waiting for managed identity propagation (30 seconds)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30

    # Build the connection string for managed identity authentication (includes User Id for user-assigned managed identity)
    $ConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$DatabaseName;Authentication=Active Directory Managed Identity;User Id=$identityClientId;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    # Set connection string in Web App configuration
    Write-Host "Setting connection string in Web App (Managed Identity authentication)..." -ForegroundColor Yellow
    az webapp config connection-string set --resource-group $ResourceGroupName --name $WebAppName --connection-string-type SQLAzure --settings DefaultConnection=$ConnectionString

    # Set environment variables (including OpenAI endpoint if created)
    Write-Host "Setting Web App configuration..." -ForegroundColor Yellow
    if ($OpenAICreated) {
        az webapp config appsettings set --resource-group $ResourceGroupName --name $WebAppName --settings ASPNETCORE_ENVIRONMENT=Production AZURE_CLIENT_ID=$identityClientId AZURE_OPENAI_ENDPOINT=$OpenAIEndpoint
    } else {
        az webapp config appsettings set --resource-group $ResourceGroupName --name $WebAppName --settings ASPNETCORE_ENVIRONMENT=Production AZURE_CLIENT_ID=$identityClientId
    }

    # Grant database permissions to the managed identity
    Write-Host "Configuring database permissions for managed identity..." -ForegroundColor Yellow
    Write-Host "Note: You will need to manually grant database permissions to the managed identity after deployment." -ForegroundColor Cyan
    Write-Host "Connect to the database and run the following commands:" -ForegroundColor Cyan
    Write-Host "CREATE USER [$ManagedIdentityName] FROM EXTERNAL PROVIDER" -ForegroundColor Cyan
    Write-Host "ALTER ROLE db_datareader ADD MEMBER [$ManagedIdentityName]" -ForegroundColor Cyan
    Write-Host "ALTER ROLE db_datawriter ADD MEMBER [$ManagedIdentityName]" -ForegroundColor Cyan

    Write-Host "Azure resources created successfully!" -ForegroundColor Green
    Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Cyan
    Write-Host "Owner: $Owner" -ForegroundColor Cyan
    Write-Host "Created By: $CreatedBy" -ForegroundColor Cyan
    Write-Host "Web App: https://$WebAppName.azurewebsites.net" -ForegroundColor Cyan
    Write-Host "SQL Server: $SqlServerName.database.windows.net" -ForegroundColor Cyan
    Write-Host "Database: $DatabaseName" -ForegroundColor Cyan
    if ($OpenAICreated) {
        Write-Host "Azure OpenAI Service: $OpenAIServiceName" -ForegroundColor Cyan
        Write-Host "OpenAI Endpoint: $OpenAIEndpoint" -ForegroundColor Cyan
    } else {
        Write-Host "Azure OpenAI Service: ❌ NOT CREATED (requires subscription approval)" -ForegroundColor Yellow
        Write-Host "Request access at: https://aka.ms/oai/access" -ForegroundColor Yellow
    }
    Write-Host "Managed Identity: $ManagedIdentityName" -ForegroundColor Cyan
    Write-Host "Entra Admin: $EntraAdminUPN" -ForegroundColor Cyan
    
    Write-Host "`nIMPORTANT: Managed Identity Authentication Setup" -ForegroundColor Yellow
    Write-Host "1. The SQL Server uses Entra ID authentication only (no SQL logins)" -ForegroundColor White
    Write-Host "2. The Web App uses User-Assigned Managed Identity for database access" -ForegroundColor White
    Write-Host "3. The managed identity has been assigned to both Web App and SQL Server" -ForegroundColor White
    Write-Host "4. Database permissions must be granted manually (see instructions above)" -ForegroundColor White
    
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Deploy your application code to the Web App" -ForegroundColor White
    Write-Host "2. Run Entity Framework migrations using Entra ID authentication" -ForegroundColor White
    Write-Host "3. Grant database permissions to managed identity:" -ForegroundColor White
    Write-Host "   - Connect to database as Entra ID admin" -ForegroundColor Gray
    Write-Host "   - Run: CREATE USER [$ManagedIdentityName] FROM EXTERNAL PROVIDER" -ForegroundColor Gray
    Write-Host "   - Run: ALTER ROLE db_datareader ADD MEMBER [$ManagedIdentityName]" -ForegroundColor Gray
    Write-Host "   - Run: ALTER ROLE db_datawriter ADD MEMBER [$ManagedIdentityName]" -ForegroundColor Gray
    if ($OpenAICreated) {
        Write-Host "4. Configure Azure OpenAI integration:" -ForegroundColor White
        Write-Host "   - Run: .\configure-openai-integration.ps1 -ResourceGroupName $ResourceGroupName -SqlServerName $SqlServerName -DatabaseName $DatabaseName" -ForegroundColor Gray
        Write-Host "5. Test your deployed application" -ForegroundColor White
    } else {
        Write-Host "4. (Optional) Request Azure OpenAI access and run setup later:" -ForegroundColor White
        Write-Host "   - Apply for access at: https://aka.ms/oai/access" -ForegroundColor Gray
        Write-Host "   - After approval, manually create OpenAI service and run configure-openai-integration.ps1" -ForegroundColor Gray
        Write-Host "5. Test your deployed application (OpenAI features will be disabled)" -ForegroundColor White
    }

} catch {
    Write-Error "Deployment failed: $($_.Exception.Message)"
    exit 1
}