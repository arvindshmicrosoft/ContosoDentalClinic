-- Validation script for SQL Database Master Key and OpenAI integration prerequisites
-- Run this script to verify your database is ready for Azure OpenAI integration

PRINT 'Validating Azure SQL Database for OpenAI Integration...';
PRINT '';

-- Check 1: Verify database master key exists
PRINT '1. Checking Database Master Key:';
IF EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    PRINT '   ✓ Database Master Key exists';
    
    -- Show master key details
    SELECT 
        name AS KeyName,
        key_length AS KeyLength,
        create_date AS CreatedDate
    FROM sys.symmetric_keys 
    WHERE name = '##MS_DatabaseMasterKey##';
END
ELSE
BEGIN
    PRINT '   ✗ Database Master Key NOT found';
    PRINT '   Action Required: Run the following command to create it:';
    PRINT '   CREATE MASTER KEY ENCRYPTION BY PASSWORD = ''YourSecurePassword123!'';';
END

PRINT '';

-- Check 2: Verify database scoped credential for OpenAI
PRINT '2. Checking Azure OpenAI Database Scoped Credential:';
IF EXISTS (SELECT * FROM sys.database_scoped_credentials WHERE name LIKE 'https://%.openai.azure.com/')
BEGIN
    PRINT '   ✓ Database Scoped Credential for Azure OpenAI exists';
    
    -- Show credential details
    SELECT 
        name AS CredentialName,
        credential_identity AS Identity,
        create_date AS CreatedDate
    FROM sys.database_scoped_credentials 
    WHERE name LIKE 'https://%.openai.azure.com/';
END
ELSE
BEGIN
    PRINT '   ✗ Database Scoped Credential for Azure OpenAI NOT found';
    PRINT '   Action Required: Run the setup-azure-openai-sql-configured.sql script';
    PRINT '   Note: Credential name should match your OpenAI service endpoint (e.g., https://your-service.openai.azure.com/)';
END

PRINT '';

-- Check 3: Verify sp_invoke_external_rest_endpoint availability
PRINT '3. Checking External REST Endpoint Capability:';
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_invoke_external_rest_endpoint' AND type = 'P')
BEGIN
    PRINT '   ✓ sp_invoke_external_rest_endpoint procedure is available';
END
ELSE
BEGIN
    PRINT '   ✗ sp_invoke_external_rest_endpoint NOT available';
    PRINT '   Note: This procedure requires Azure SQL Database (not SQL Server on-premises)';
END

PRINT '';

-- Check 4: List all database scoped credentials for reference
PRINT '4. All Database Scoped Credentials:';
IF EXISTS (SELECT * FROM sys.database_scoped_credentials)
BEGIN
    SELECT 
        name AS CredentialName,
        credential_identity AS Identity,
        create_date AS CreatedDate
    FROM sys.database_scoped_credentials
    ORDER BY create_date DESC;
END
ELSE
BEGIN
    PRINT '   No database scoped credentials found.';
END

PRINT '';
PRINT 'Validation Complete.';
PRINT 'If all checks pass with ✓, your database is ready for Azure OpenAI integration.';