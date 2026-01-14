# Deploy Application Code to Azure Web App
# Run this script after running deploy-to-azure.ps1 to deploy your application code

param(
    [Parameter(Mandatory=$true)]
    [string]$WebAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName
)

Write-Host "Deploying Contoso University Dental Clinic to Azure Web App..." -ForegroundColor Green

try {
    # Build the application in Release mode
    Write-Host "Building application in Release mode..." -ForegroundColor Yellow
    dotnet build DentalClinicWebApp.csproj --configuration Release

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    # Publish the application
    Write-Host "Publishing application..." -ForegroundColor Yellow
    dotnet publish DentalClinicWebApp.csproj --configuration Release --output ./publish

    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed"
    }

    # Create deployment package
    Write-Host "Creating deployment package..." -ForegroundColor Yellow
    $publishPath = "./publish"
    $zipPath = "./deployment-package.zip"
    
    # Remove existing zip if it exists
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    # Create zip package
    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force

    # Deploy to Azure Web App using az webapp deployment
    Write-Host "Deploying to Azure Web App: $WebAppName..." -ForegroundColor Yellow
    az webapp deploy --resource-group $ResourceGroupName --name $WebAppName --src-path $zipPath --type zip

    if ($LASTEXITCODE -ne 0) {
        throw "Deployment failed"
    }

    Write-Host "Application deployed successfully!" -ForegroundColor Green
    Write-Host "Your app should be available at: https://$WebAppName.azurewebsites.net" -ForegroundColor Cyan
    
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Run Entity Framework migrations to create database schema" -ForegroundColor White
    Write-Host "2. Grant database permissions to the managed identity" -ForegroundColor White
    Write-Host "3. Test your deployed application" -ForegroundColor White

    # Clean up
    Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
    Remove-Item $zipPath -Force
    Remove-Item $publishPath -Recurse -Force

} catch {
    Write-Error "Deployment failed: $($_.Exception.Message)"
    
    # Clean up on error
    if (Test-Path "./deployment-package.zip") {
        Remove-Item "./deployment-package.zip" -Force
    }
    if (Test-Path "./publish") {
        Remove-Item "./publish" -Recurse -Force
    }
    
    exit 1
}