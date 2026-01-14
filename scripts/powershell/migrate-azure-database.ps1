# Entity Framework Migration Script for Azure SQL Database
# Run this script after deploying the web app to Azure

param(
    [Parameter(Mandatory=$true)]
    [string]$SqlServerName,
    
    [Parameter(Mandatory=$true)]
    [string]$DatabaseName
)

Write-Host "Running Entity Framework migrations against Azure SQL Database (Entra ID authentication)..." -ForegroundColor Green

# Build the connection string for Entra ID authentication
$ConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$DatabaseName;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "Connection String: Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$DatabaseName;Authentication=Active Directory Default" -ForegroundColor Cyan

try {
    # Update the database with Entity Framework migrations
    Write-Host "Applying Entity Framework migrations..." -ForegroundColor Yellow
    dotnet ef database update --connection $ConnectionString

    Write-Host "Entity Framework migrations completed successfully!" -ForegroundColor Green
    Write-Host "Your database is now ready with all tables and seed data." -ForegroundColor Cyan

} catch {
    Write-Error "Migration failed: $($_.Exception.Message)"
    Write-Host "Please check your connection string and ensure the Azure SQL Database is accessible." -ForegroundColor Red
    exit 1
}

Write-Host "`nDatabase setup complete!" -ForegroundColor Green
Write-Host "You can now test your deployed application." -ForegroundColor Cyan