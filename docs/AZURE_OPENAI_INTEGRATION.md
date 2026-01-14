# Azure OpenAI Integration with Azure SQL Database

This document explains how to integrate Azure OpenAI with Azure SQL Database using User-Assigned Managed Identity for passwordless authentication.

## Overview

The integration enables Azure SQL Database to directly call Azure OpenAI APIs without storing API keys or passwords. This is achieved using:

1. **User-Assigned Managed Identity (UAMI)** - Provides identity for authentication
2. **Database Scoped Credential** - Configures SQL to use the managed identity
3. **sp_invoke_external_rest_endpoint** - Built-in SQL procedure for making HTTP calls

## Prerequisites

- Azure SQL Database (not SQL Server on VM)
- Azure OpenAI service in the same or accessible region
- User-Assigned Managed Identity with appropriate permissions
- SQL Database authentication configured for Entra ID

## Architecture

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│   Azure SQL DB      │    │   Azure OpenAI       │    │   Web Application   │
│                     │    │                      │    │                     │
│ ┌─────────────────┐ │    │ ┌──────────────────┐ │    │ ┌─────────────────┐ │
│ │ Stored Proc     │ │────│ │ GPT-3.5/GPT-4    │ │    │ │ ASP.NET Core    │ │
│ │ sp_CallOpenAI   │ │    │ │ Deployment       │ │    │ │ Application     │ │
│ └─────────────────┘ │    │ └──────────────────┘ │    │ └─────────────────┘ │
│           │         │    │                      │    │           │         │
│ ┌─────────▼───────┐ │    │                      │    │ ┌─────────▼───────┐ │
│ │ Database Scoped │ │    │                      │    │ │ Same UAMI       │ │
│ │ Credential      │ │    │                      │    │ │ for DB Access   │ │
│ └─────────────────┘ │    │                      │    │ └─────────────────┘ │
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
           │                           ▲                           │
           │                           │                           │
           └───────────────────────────┼───────────────────────────┘
                                       │
                              ┌────────▼────────┐
                              │ User-Assigned   │
                              │ Managed Identity│
                              │ + OpenAI Role   │
                              └─────────────────┘
```

## Deployment

### 1. Automated Deployment

The main deployment script (`scripts/powershell/deploy-to-azure.ps1`) automatically:

- Creates Azure OpenAI service in the best available region
- Automatically selects OpenAI-supported regions (East US, West US, West Europe, etc.)
- Maps your preferred location to the nearest OpenAI region if needed
- Assigns the managed identity "Cognitive Services OpenAI User" role
- Configures the web app with OpenAI endpoint settings

> **Region Selection**: Azure OpenAI is only available in specific regions. The deployment script intelligently selects the best region based on your preferred location, ensuring successful deployment even if your main resource group is in a region that doesn't support OpenAI.

### 2. Configure OpenAI Model Deployment

```powershell
.\scripts\powershell\configure-openai-integration.ps1 `
    -ResourceGroupName "rg-dental-clinic" `
    -SqlServerName "sql-dental-clinic" `
    -DatabaseName "DentalClinicDb" `
    -OpenAIDeploymentName "gpt-4"
```

### 3. Set Up Database Integration

Run the generated SQL script `setup-azure-openai-sql-configured.sql` in your database.

### 4. Validate Setup (Optional)

To verify your database is properly configured, run the validation script:
```sql
-- Run this in your Azure SQL Database
\i validate-sql-master-key.sql
```

This will check for:
- Database Master Key existence
- Database Scoped Credential configuration  
- External REST endpoint availability

## Usage Examples

### Basic OpenAI Call

```sql
DECLARE @response NVARCHAR(MAX);
DECLARE @result INT;

EXEC @result = sp_CallAzureOpenAI 
    @endpoint = N'https://your-openai-service.openai.azure.com',
    @deployment_name = N'gpt-4',
    @user_message = N'What are the best practices for dental hygiene?',
    @max_tokens = 200,
    @response = @response OUTPUT;

-- Extract just the AI response content
SELECT dbo.fn_ExtractOpenAIContent(@response) AS AIResponse;
```

### Integration with Dental Clinic Data

```sql
-- Example: Generate treatment recommendations based on patient data
CREATE OR ALTER PROCEDURE sp_GetTreatmentRecommendations
    @PatientId NVARCHAR(450)
AS
BEGIN
    DECLARE @patientInfo NVARCHAR(MAX);
    DECLARE @aiResponse NVARCHAR(MAX);
    DECLARE @result INT;
    
    -- Get patient information
    SELECT @patientInfo = 
        'Patient Age: ' + CAST(DATEDIFF(YEAR, DateOfBirth, GETDATE()) AS NVARCHAR) + 
        ', Medical History: ' + ISNULL(
            (SELECT STRING_AGG(TreatmentType, ', ') 
             FROM PatientRecords 
             WHERE PatientId = @PatientId), 
            'No previous records'
        )
    FROM AspNetUsers 
    WHERE Id = @PatientId;
    
    -- Call OpenAI for treatment recommendations
    EXEC @result = sp_CallAzureOpenAI 
        @endpoint = N'https://your-openai-service.openai.azure.com',
        @deployment_name = N'gpt-4',
        @user_message = @patientInfo + '. Based on this dental patient information, what preventive care recommendations would you suggest? Keep it concise and professional.',
        @max_tokens = 300,
        @response = @aiResponse OUTPUT;
    
    SELECT 
        @PatientId AS PatientId,
        @patientInfo AS PatientInfo,
        dbo.fn_ExtractOpenAIContent(@aiResponse) AS AIRecommendations,
        @result AS StatusCode;
END;
```

### Automated Report Generation

```sql
-- Example: Generate appointment summaries
CREATE OR ALTER PROCEDURE sp_GenerateAppointmentSummary
    @AppointmentId INT
AS
BEGIN
    DECLARE @appointmentDetails NVARCHAR(MAX);
    DECLARE @aiSummary NVARCHAR(MAX);
    DECLARE @result INT;
    
    -- Gather appointment information
    SELECT @appointmentDetails = 
        'Appointment Date: ' + FORMAT(AppointmentDate, 'yyyy-MM-dd') + 
        ', Service: ' + s.Name + 
        ', Duration: ' + CAST(s.DurationMinutes AS NVARCHAR) + ' minutes' +
        ', Patient: ' + u.FirstName + ' ' + u.LastName
    FROM Appointments a
    JOIN Services s ON a.ServiceId = s.Id
    JOIN AspNetUsers u ON a.PatientId = u.Id
    WHERE a.Id = @AppointmentId;
    
    -- Generate AI summary
    EXEC @result = sp_CallAzureOpenAI 
        @endpoint = N'https://your-openai-service.openai.azure.com',
        @deployment_name = N'gpt-4',
        @user_message = @appointmentDetails + '. Create a brief, professional appointment summary for dental clinic records.',
        @max_tokens = 150,
        @response = @aiSummary OUTPUT;
    
    SELECT 
        @AppointmentId AS AppointmentId,
        dbo.fn_ExtractOpenAIContent(@aiSummary) AS GeneratedSummary;
END;
```

## Security Considerations

### Managed Identity Benefits

- **No stored credentials** - No API keys in connection strings or code
- **Automatic token refresh** - Azure handles token lifecycle
- **Principle of least privilege** - Role assignments can be scoped precisely
- **Audit trail** - All API calls are logged with identity information

### Data Privacy

- **Patient data protection** - Ensure HIPAA compliance when sending data to OpenAI
- **Data residency** - Choose OpenAI regions that meet your compliance requirements
- **Logging** - Monitor all OpenAI API calls for audit purposes

### Best Practices

1. **Sanitize input** - Remove PII when possible before sending to OpenAI
2. **Validate responses** - Always validate AI-generated content before using
3. **Rate limiting** - Implement appropriate usage controls
4. **Error handling** - Handle API failures gracefully
5. **Cost monitoring** - Track OpenAI usage and costs

## Troubleshooting

### Common Issues

1. **Database scoped credential errors**
   ```
   Error: The database scoped credential cannot be used to invoke an external rest endpoint
   Solution: Credential name must match your OpenAI service endpoint
   Correct format: [https://your-service-name.openai.azure.com/]
   Incorrect format: [https://cognitiveservices.azure.com/]
   ```

2. **Authentication failures**
   ```
   Error: Authentication failed
   Solution: Verify managed identity has "Cognitive Services OpenAI User" role
   ```

3. **Deployment not found**
   ```
   Error: The API deployment for this resource does not exist
   Solution: Create model deployment using configure-openai-integration.ps1
   ```

4. **Network connectivity**
   ```
   Error: Request timeout
   Solution: Check firewall rules and network connectivity from SQL to OpenAI
   ```

### Verification Steps

1. **Check managed identity assignment**
   ```bash
   az role assignment list --assignee <managed-identity-object-id> --scope <resource-group-scope>
   ```

2. **Verify OpenAI deployment**
   ```bash
   az cognitiveservices account deployment list --name <openai-service-name> --resource-group <rg-name>
   ```

3. **Test database credential**
   ```sql
   SELECT * FROM sys.database_scoped_credentials;
   ```

## Performance Considerations

- **Token limits** - GPT-4: 128K tokens (context window for turbo-2024-04-09 version)
- **Rate limits** - Standard deployment: 40K tokens/min for GPT-4 (sufficient for dental clinic usage)
- **Response time** - GPT-4: 2-5 seconds per request (reliable performance)
- **Quality vs Speed** - GPT-4 provides excellent reasoning capabilities for medical use cases
- **Caching** - Recommended for GPT-4 to optimize costs and response times

## Cost Management

- **GPT-4** (Current Default): ~$0.03 input / $0.06 output per 1K tokens
- **GPT-3.5-turbo** (Alternative): ~$0.002 per 1K tokens (cheaper but less capable for medical content)
- **Cost-Effectiveness**: GPT-4 provides reliable medical-grade responses with predictable costs
- **Monitoring**: Use Azure Cost Management to track spending
- **Best Practices**: Cache responses, optimize prompt lengths, consider GPT-3.5 for simple tasks

## Support and Resources

- [Azure OpenAI Documentation](https://docs.microsoft.com/azure/cognitive-services/openai/)
- [SQL Database External REST Endpoints](https://docs.microsoft.com/azure/azure-sql/database/external-rest-endpoints)
- [Managed Identity Best Practices](https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/best-practice-recommendations)