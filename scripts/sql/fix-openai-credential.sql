-- Fix for "database scoped credential cannot be used to invoke external rest endpoint" error
-- This script corrects the credential format for Azure OpenAI integration

PRINT 'Fixing Azure OpenAI Database Scoped Credential...';
PRINT '';

-- Step 1: Check current credentials
PRINT '1. Current Database Scoped Credentials:';
SELECT 
    name AS CredentialName,
    credential_identity AS Identity,
    create_date AS CreatedDate
FROM sys.database_scoped_credentials
ORDER BY create_date DESC;

PRINT '';

-- Step 2: Remove incorrect generic credential if it exists
IF EXISTS (SELECT * FROM sys.database_scoped_credentials WHERE name = 'https://cognitiveservices.azure.com/')
BEGIN
    PRINT '2. Removing incorrect generic credential...';
    DROP DATABASE SCOPED CREDENTIAL [https://cognitiveservices.azure.com/];
    PRINT '   ✓ Removed https://cognitiveservices.azure.com/ credential';
END
ELSE
BEGIN
    PRINT '2. No incorrect generic credential found.';
END

PRINT '';
PRINT '3. Next Steps:';
PRINT '   Run your configured setup script (setup-azure-openai-sql-configured.sql)';
PRINT '   This will create the correct credential with format:';
PRINT '   [https://your-openai-service-name.openai.azure.com/]';
PRINT '';
PRINT 'Why this matters:';
PRINT '- Database scoped credentials must match the base URL of the service being called';
PRINT '- Generic "cognitiveservices.azure.com" does not match specific OpenAI service endpoints';
PRINT '- Each OpenAI service has a unique endpoint: your-service.openai.azure.com';
PRINT '';
PRINT 'Credential fix completed. Please run your setup script next.';