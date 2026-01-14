-- Setup Azure OpenAI Integration with Azure SQL Database
-- This script creates the necessary database scoped credential and tests the connection
-- Run this script after deploying the Azure resources and granting database permissions to the managed identity

-- Step 0: Ensure database master key exists (required for database scoped credentials)
-- IMPORTANT: Replace the password below with your own secure password before running!
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

-- Step 1: Create database scoped credential for Azure OpenAI access
-- The credential name must match the base URL of your OpenAI service endpoint

-- Drop old credential if it exists (cleanup from previous incorrect configuration)
IF EXISTS (SELECT * FROM sys.database_scoped_credentials WHERE name = 'https://cognitiveservices.azure.com/')
    DROP DATABASE SCOPED CREDENTIAL [https://cognitiveservices.azure.com/];

-- Create the correct credential with the actual OpenAI service endpoint
CREATE DATABASE SCOPED CREDENTIAL [https://{your-openai-service-name}.openai.azure.com/]
WITH IDENTITY = 'Managed Identity',
     SECRET = '{"resourceid":"https://cognitiveservices.azure.com/"}';
GO

-- Step 2: Test the credential by calling Azure OpenAI
-- This is a simple test to verify the connection works
-- You'll need to replace the endpoint URL with your actual OpenAI service endpoint
DECLARE @url NVARCHAR(4000) = N'https://{your-openai-service-name}.openai.azure.com/openai/deployments/{deployment-name}/chat/completions?api-version=2024-02-01';
DECLARE @headers NVARCHAR(300) = N'{"Content-Type":"application/json"}';
DECLARE @payload NVARCHAR(MAX) = N'{
    "messages": [
        {
            "role": "user",
            "content": "Hello, this is a test from Azure SQL Database. Please respond with a simple greeting."
        }
    ],
    "max_tokens": 50,
    "temperature": 0.7
}';

DECLARE @ret INT, @response NVARCHAR(MAX);

-- Make the REST API call to Azure OpenAI
EXEC @ret = sp_invoke_external_rest_endpoint 
    @url = @url,
    @method = 'POST',
    @headers = @headers,
    @payload = @payload,
    @credential = [https://{your-openai-service-name}.openai.azure.com/],
    @response = @response OUTPUT;

-- Display the results
SELECT @ret AS ReturnCode, @response AS ResponseFromOpenAI;
GO

-- Step 3: Create a reusable function for OpenAI calls (example)
-- This creates a stored procedure that can be used to call Azure OpenAI from SQL
CREATE OR ALTER PROCEDURE sp_CallAzureOpenAI
    @endpoint NVARCHAR(4000),
    @deployment_name NVARCHAR(100),
    @user_message NVARCHAR(MAX),
    @max_tokens INT = 150,
    @temperature FLOAT = 0.7,
    @response NVARCHAR(MAX) OUTPUT
AS
BEGIN
    DECLARE @url NVARCHAR(4000) = @endpoint + '/openai/deployments/' + @deployment_name + '/chat/completions?api-version=2024-02-01';
    DECLARE @headers NVARCHAR(300) = N'{"Content-Type":"application/json"}';
    DECLARE @payload NVARCHAR(MAX) = N'{
        "messages": [
            {
                "role": "user",
                "content": "' + REPLACE(@user_message, '"', '\"') + '"
            }
        ],
        "max_tokens": ' + CAST(@max_tokens AS NVARCHAR(10)) + ',
        "temperature": ' + CAST(@temperature AS NVARCHAR(10)) + '
    }';

    DECLARE @ret INT;

    -- Make the REST API call to Azure OpenAI
    EXEC @ret = sp_invoke_external_rest_endpoint 
        @url = @url,
        @method = 'POST',
        @headers = @headers,
        @payload = @payload,
        @credential = [https://{your-openai-service-name}.openai.azure.com/],
        @response = @response OUTPUT;

    -- Return the status code
    RETURN @ret;
END;
GO

-- Step 4: Example usage of the stored procedure
-- Uncomment and modify the following to test the stored procedure:
/*
DECLARE @openai_response NVARCHAR(MAX);
DECLARE @result INT;

EXEC @result = sp_CallAzureOpenAI 
    @endpoint = N'https://{your-openai-service-name}.openai.azure.com',
    @deployment_name = N'{your-deployment-name}',
    @user_message = N'What are the best practices for dental hygiene?',
    @max_tokens = 200,
    @temperature = 0.7,
    @response = @openai_response OUTPUT;

SELECT @result AS ReturnCode, @openai_response AS OpenAIResponse;
*/

-- Step 5: Create a function to extract just the content from OpenAI response
CREATE OR ALTER FUNCTION fn_ExtractOpenAIContent(@json_response NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    DECLARE @content NVARCHAR(MAX);
    
    -- Extract the content from the OpenAI JSON response
    -- This assumes the standard OpenAI API response format
    SELECT @content = JSON_VALUE(@json_response, '$.result.choices[0].message.content');
    
    RETURN @content;
END;
GO

PRINT 'Azure OpenAI integration setup completed successfully!';

-- Verify the master key and credential were created properly
PRINT 'Verification:';
IF EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
    PRINT '✓ Database Master Key exists';
ELSE
    PRINT '✗ Database Master Key NOT found - this is required for database scoped credentials';

IF EXISTS (SELECT * FROM sys.database_scoped_credentials WHERE name = 'https://{your-openai-service-name}.openai.azure.com/')
    PRINT '✓ Database Scoped Credential for Azure OpenAI exists';
ELSE
    PRINT '✗ Database Scoped Credential NOT found - check for errors above';

PRINT 'Next steps:';
PRINT '1. Replace {your-openai-service-name} with your actual Azure OpenAI service name';
PRINT '2. Create a deployment in your Azure OpenAI service (e.g., gpt-35-turbo or gpt-4)';
PRINT '3. Replace {deployment-name} with your actual deployment name';
PRINT '4. Test the connection using the provided examples';