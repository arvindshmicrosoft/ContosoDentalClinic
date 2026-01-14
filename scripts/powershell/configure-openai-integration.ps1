# Configure Azure OpenAI Integration with SQL Database
# Run this script after the main deployment to complete the OpenAI setup

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$SqlServerName,
    
    [Parameter(Mandatory=$true)]
    [string]$DatabaseName,
    
    [Parameter(Mandatory=$false)]
    [string]$OpenAIDeploymentName = "gpt-4",
    
    [Parameter(Mandatory=$false)]
    [string]$OpenAIModelName = "gpt-4",
    
    [Parameter(Mandatory=$false)]
    [string]$OpenAIModelVersion = "turbo-2024-04-09"
)

Write-Host "Configuring Azure OpenAI Integration with SQL Database..." -ForegroundColor Green
Write-Host "Using GPT-4 model for enhanced reasoning and medical accuracy" -ForegroundColor Cyan

try {
    # Get the OpenAI service name (constructed the same way as in deploy-to-azure.ps1)
    $OpenAIServiceName = "$ResourceGroupName-openai-svc"
    
    # Create a model deployment in Azure OpenAI
    Write-Host "Creating OpenAI model deployment: $OpenAIDeploymentName ($OpenAIModelName)" -ForegroundColor Yellow
    Write-Host "Note: GPT-4 deployment may take several minutes to complete..." -ForegroundColor Yellow
    az cognitiveservices account deployment create `
        --resource-group $ResourceGroupName `
        --name $OpenAIServiceName `
        --deployment-name $OpenAIDeploymentName `
        --model-name $OpenAIModelName `
        --model-version $OpenAIModelVersion `
        --model-format OpenAI `
        --sku-capacity 20 `
        --sku-name Standard

    # Get the OpenAI endpoint
    $OpenAIEndpoint = az cognitiveservices account show --resource-group $ResourceGroupName --name $OpenAIServiceName --query "properties.endpoint" -o tsv
    
    Write-Host "OpenAI Service Endpoint: $OpenAIEndpoint" -ForegroundColor Cyan
    
    # Create a customized SQL script with the actual service names
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $SqlScriptPath = Join-Path (Split-Path -Parent $ScriptDir) "sql\setup-azure-openai-sql.sql"
    $SqlScriptContent = Get-Content $SqlScriptPath -Raw
    
    # Add master key creation at the beginning if not exists
    # IMPORTANT: The password below should be changed for production use
    $MasterKeyScript = @"
-- Ensure database master key exists (required for database scoped credentials)
-- IMPORTANT: Change this password to a secure value for production!
IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = '<YOUR_SECURE_MASTER_KEY_PASSWORD>';
    PRINT 'Database Master Key created successfully.';
END
ELSE
BEGIN
    PRINT 'Database Master Key already exists.';
END
GO

"@
    
    # Combine master key script with the main script
    $SqlScriptContent = $MasterKeyScript + $SqlScriptContent
    $SqlScriptContent = $SqlScriptContent -replace '\{your-openai-service-name\}', $OpenAIServiceName
    $SqlScriptContent = $SqlScriptContent -replace '\{deployment-name\}', $OpenAIDeploymentName
    
    # Save the customized script
    $CustomizedScriptPath = "setup-azure-openai-sql-configured.sql"
    Set-Content -Path $CustomizedScriptPath -Value $SqlScriptContent -Encoding UTF8
    
    Write-Host "Created customized SQL script: $CustomizedScriptPath" -ForegroundColor Green
    
    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "1. Connect to your Azure SQL Database using Azure Data Studio or SSMS" -ForegroundColor White
    Write-Host "2. Ensure you're connected as the Entra ID admin" -ForegroundColor White
    Write-Host "3. Run the customized SQL script: $CustomizedScriptPath" -ForegroundColor White
    Write-Host "4. Test the OpenAI integration using the provided examples" -ForegroundColor White
    
    Write-Host "`nConfiguration Summary:" -ForegroundColor Yellow
    Write-Host "OpenAI Service: $OpenAIServiceName" -ForegroundColor Cyan
    Write-Host "OpenAI Endpoint: $OpenAIEndpoint" -ForegroundColor Cyan
    Write-Host "Deployment Name: $OpenAIDeploymentName" -ForegroundColor Cyan
    Write-Host "Model: $OpenAIModelName ($OpenAIModelVersion)" -ForegroundColor Cyan
    Write-Host "SQL Server: $SqlServerName.database.windows.net" -ForegroundColor Cyan
    Write-Host "Database: $DatabaseName" -ForegroundColor Cyan
    
    Write-Host "`nExample Test Query (run in SQL after setup):" -ForegroundColor Yellow
    Write-Host "DECLARE @response NVARCHAR(MAX);" -ForegroundColor Gray
    Write-Host "DECLARE @result INT;" -ForegroundColor Gray
    Write-Host "EXEC @result = sp_CallAzureOpenAI" -ForegroundColor Gray
    Write-Host "    @endpoint = N'$OpenAIEndpoint'," -ForegroundColor Gray
    Write-Host "    @deployment_name = N'$OpenAIDeploymentName'," -ForegroundColor Gray
    Write-Host "    @user_message = N'What are the best practices for dental care?'," -ForegroundColor Gray
    Write-Host "    @response = @response OUTPUT;" -ForegroundColor Gray
    Write-Host "SELECT @result AS ReturnCode, dbo.fn_ExtractOpenAIContent(@response) AS AIResponse;" -ForegroundColor Gray

} catch {
    Write-Error "Configuration failed: $($_.Exception.Message)"
    exit 1
}