@echo off
REM Simple deployment script for Contoso University Dental Clinic
REM This script builds and packages the application for Azure deployment

echo Building Contoso University Dental Clinic for Azure deployment...

REM Restore NuGet packages
echo Restoring NuGet packages...
dotnet restore
if %errorlevel% neq 0 goto error

REM Build the project in Release mode
echo Building project in Release mode...
dotnet build --configuration Release --no-restore
if %errorlevel% neq 0 goto error

REM Publish the application
echo Publishing application...
dotnet publish --configuration Release --output ./publish --no-build
if %errorlevel% neq 0 goto error

REM Create deployment package
echo Creating deployment package...
if exist dental-clinic-app.zip del dental-clinic-app.zip
powershell -Command "Compress-Archive -Path ./publish/* -DestinationPath dental-clinic-app.zip -Force"
if %errorlevel% neq 0 goto error

echo.
echo ===================================================================
echo BUILD AND PACKAGING COMPLETED SUCCESSFULLY!
echo ===================================================================
echo.
echo Deployment package created: dental-clinic-app.zip
echo.
echo Next steps:
echo 1. Deploy to Azure using Azure CLI or Azure Portal
echo 2. Configure connection string in Azure App Service
echo 3. Run database migrations
echo.
echo For Azure CLI deployment, run:
echo az webapp deployment source config-zip --resource-group [rg-name] --name [webapp-name] --src dental-clinic-app.zip
echo.
goto end

:error
echo.
echo ===================================================================
echo BUILD FAILED!
echo ===================================================================
echo Please check the error messages above and fix any issues.
pause
exit /b 1

:end
pause