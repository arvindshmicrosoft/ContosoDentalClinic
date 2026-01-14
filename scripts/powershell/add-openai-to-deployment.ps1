# Add Azure OpenAI to existing deployment
# Run this script after getting Azure OpenAI approval to add OpenAI services to your existing deployment

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$WebAppName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US"
)

Write-Host "Adding Azure OpenAI Service to existing deployment..." -ForegroundColor Green

# Function to find the best available Azure OpenAI region
function Get-AvailableOpenAILocation {
    param([string]$PreferredLocation)
    
    # Known Azure OpenAI regions (as of October 2025) in order of preference
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
    
    # Default fallback
    Write-Host "Using default OpenAI region: East US (preferred location '$PreferredLocation' not supported)" -ForegroundColor Yellow
    return "East US"
}

try {
    # Check if already logged in to Azure
    Write-Host "Checking Azure login status..." -ForegroundColor Yellow
    $account = az account show 2>$null
    if (-not $account) {
        Write-Host "Please login to Azure..." -ForegroundColor Yellow
        az login
    }

    # Get the existing managed identity
    $ManagedIdentityName = "$WebAppName-identity"
    Write-Host "Finding existing managed identity: $ManagedIdentityName" -ForegroundColor Yellow
    $identityResult = az identity show --name $ManagedIdentityName --resource-group $ResourceGroupName | ConvertFrom-Json
    
    if (-not $identityResult) {
        throw "Managed identity $ManagedIdentityName not found. Please ensure the main deployment completed successfully."
    }
    
    $identityPrincipalId = $identityResult.principalId

    # Create Azure OpenAI resource
    $OpenAIServiceName = "$ResourceGroupName-openai-svc"
    
    # Find the best available region for OpenAI
    $OpenAILocation = Get-AvailableOpenAILocation -PreferredLocation $Location
    Write-Host "Selected OpenAI region: $OpenAILocation" -ForegroundColor Cyan
    
    Write-Host "Creating Azure OpenAI Service: $OpenAIServiceName" -ForegroundColor Yellow
    az cognitiveservices account create --name $OpenAIServiceName --resource-group $ResourceGroupName --location $OpenAILocation --kind OpenAI --sku S0 --custom-domain $OpenAIServiceName

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create Azure OpenAI service. Please check if your subscription has OpenAI access approval."
    }

    Write-Host "✅ Azure OpenAI Service created successfully!" -ForegroundColor Green

    # Assign "Cognitive Services OpenAI User" role to the managed identity
    Write-Host "Assigning Cognitive Services OpenAI User role to managed identity..." -ForegroundColor Yellow
    az role assignment create --assignee $identityPrincipalId --role "Cognitive Services OpenAI User" --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroupName"

    # Get OpenAI endpoint and update web app settings
    $OpenAIEndpoint = "https://$OpenAIServiceName.openai.azure.com/"
    
    Write-Host "Adding OpenAI endpoint to web app configuration..." -ForegroundColor Yellow
    az webapp config appsettings set --resource-group $ResourceGroupName --name $WebAppName --settings AZURE_OPENAI_ENDPOINT=$OpenAIEndpoint

    Write-Host "`nAzure OpenAI Service added successfully!" -ForegroundColor Green
    Write-Host "OpenAI Service: $OpenAIServiceName" -ForegroundColor Cyan
    Write-Host "OpenAI Endpoint: $OpenAIEndpoint" -ForegroundColor Cyan
    
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Configure model deployment:" -ForegroundColor White
    Write-Host "   .\configure-openai-integration.ps1 -ResourceGroupName $ResourceGroupName -SqlServerName {your-sql-server} -DatabaseName {your-database}" -ForegroundColor Gray
    Write-Host "2. Set up SQL Database integration using the generated scripts" -ForegroundColor White
    Write-Host "3. Test the OpenAI integration" -ForegroundColor White

} catch {
    Write-Error "Failed to add Azure OpenAI: $($_.Exception.Message)"
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Ensure you have applied for and received Azure OpenAI access: https://aka.ms/oai/access" -ForegroundColor White
    Write-Host "2. Check that your subscription has the required quotas" -ForegroundColor White
    Write-Host "3. Verify the resource group and managed identity exist" -ForegroundColor White
    exit 1
}