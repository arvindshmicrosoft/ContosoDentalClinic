using DentalClinicWebApp.Data;
using DentalClinicWebApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace DentalClinicWebApp.Services
{
    public interface IMedicalHistorySummarizationService
    {
        Task<PatientMedicalHistorySummary?> GetOrCreateSummaryAsync(string patientId, string currentUserId);
        Task<string> GenerateAiSummaryAsync(IEnumerable<PatientRecord> patientRecords);
    }

    public class MedicalHistorySummarizationService : IMedicalHistorySummarizationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MedicalHistorySummarizationService> _logger;
        private readonly IConfiguration _configuration;

        public MedicalHistorySummarizationService(
            ApplicationDbContext context, 
            ILogger<MedicalHistorySummarizationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<PatientMedicalHistorySummary?> GetOrCreateSummaryAsync(string patientId, string currentUserId)
        {
            try
            {
                // Check if summary already exists
                var existingSummary = await _context.PatientMedicalHistorySummaries
                    .FirstOrDefaultAsync(s => s.PatientId == patientId && s.IsActive);

                if (existingSummary != null)
                {
                    _logger.LogInformation("Found existing medical history summary for patient {PatientId}", patientId);
                    return existingSummary;
                }

                // Get all patient records to summarize
                var patientRecords = await _context.PatientRecords
                    .Where(pr => pr.PatientId == patientId && pr.IsActive)
                    .OrderBy(pr => pr.RecordDate)
                    .ToListAsync();

                if (!patientRecords.Any())
                {
                    _logger.LogInformation("No patient records found for patient {PatientId}", patientId);
                    return null;
                }

                // Generate AI summary
                var aiSummary = await GenerateAiSummaryAsync(patientRecords);

                if (string.IsNullOrEmpty(aiSummary))
                {
                    _logger.LogWarning("Failed to generate AI summary for patient {PatientId}", patientId);
                    return null;
                }

                // Create and save the summary
                var newSummary = new PatientMedicalHistorySummary
                {
                    PatientId = patientId,
                    AiGeneratedSummary = aiSummary,
                    CreatedByUserId = currentUserId,
                    SourceRecordsCount = patientRecords.Count,
                    ModelUsed = "gpt-4",
                    CreatedDate = DateTime.UtcNow,
                    LastUpdatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.PatientMedicalHistorySummaries.Add(newSummary);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new medical history summary for patient {PatientId} with {RecordCount} records", 
                    patientId, patientRecords.Count);

                return newSummary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating medical history summary for patient {PatientId}", patientId);
                return null;
            }
        }

        public async Task<string> GenerateAiSummaryAsync(IEnumerable<PatientRecord> patientRecords)
        {
            try
            {
                var recordsList = patientRecords.ToList();
                if (!recordsList.Any())
                {
                    return string.Empty;
                }

                // Check if we're running on Azure SQL Database
                var isAzureSqlDatabase = await IsAzureSqlDatabaseAsync();
                
                if (isAzureSqlDatabase)
                {
                    _logger.LogInformation("Detected Azure SQL Database - using real OpenAI integration");
                    
                    // Prepare the medical history text for AI processing
                    var medicalHistoryText = PreparePatientRecordsForSummarization(recordsList);
                    
                    // Call Azure OpenAI via SQL Database stored procedure
                    var aiSummary = await CallAzureOpenAIFromSqlAsync(medicalHistoryText);
                    
                    if (!string.IsNullOrEmpty(aiSummary))
                    {
                        _logger.LogInformation("Successfully generated AI summary using Azure OpenAI");
                        return aiSummary;
                    }
                    else
                    {
                        _logger.LogWarning("Azure OpenAI call failed, falling back to mock summary");
                    }
                }
                else
                {
                    _logger.LogInformation("Detected local SQL Server - using mock implementation");
                }

                // Fallback to mock implementation for local development or if OpenAI fails
                var mockSummary = await GenerateMockSummaryAsync(recordsList);
                return mockSummary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI summary for patient records");
                return string.Empty;
            }
        }

        private string PreparePatientRecordsForSummarization(List<PatientRecord> records)
        {
            var summaryText = "Patient Medical History Summary Request:\n\n";
            
            foreach (var record in records.OrderBy(r => r.RecordDate))
            {
                summaryText += $"Date: {record.RecordDate:yyyy-MM-dd}\n";
                summaryText += $"Title: {record.Title}\n";
                
                if (!string.IsNullOrEmpty(record.MedicalHistory))
                    summaryText += $"Medical History: {record.MedicalHistory}\n";
                
                if (!string.IsNullOrEmpty(record.Allergies))
                    summaryText += $"Allergies: {record.Allergies}\n";
                
                if (!string.IsNullOrEmpty(record.Medications))
                    summaryText += $"Medications: {record.Medications}\n";
                
                if (!string.IsNullOrEmpty(record.TreatmentPlan))
                    summaryText += $"Treatment Plan: {record.TreatmentPlan}\n";
                
                if (!string.IsNullOrEmpty(record.Notes))
                    summaryText += $"Notes: {record.Notes}\n";
                
                summaryText += "\n---\n\n";
            }

            return summaryText;
        }

        private async Task<string> GenerateMockSummaryAsync(List<PatientRecord> records)
        {
            // Mock implementation for testing - replace with Azure OpenAI later
            await Task.Delay(100); // Simulate API call delay

            var recordCount = records.Count;
            var dateRange = records.Any() ? 
                $"{records.Min(r => r.RecordDate):yyyy-MM-dd} to {records.Max(r => r.RecordDate):yyyy-MM-dd}" : 
                "Unknown";

            var uniqueTreatments = records
                .Where(r => !string.IsNullOrEmpty(r.Title))
                .Select(r => r.Title)
                .Distinct()
                .ToList();

            var knownAllergies = records
                .Where(r => !string.IsNullOrEmpty(r.Allergies))
                .SelectMany(r => r.Allergies!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(a => a.Trim())
                .Distinct()
                .ToList();

            var currentMedications = records
                .Where(r => !string.IsNullOrEmpty(r.Medications))
                .SelectMany(r => r.Medications!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(m => m.Trim())
                .Distinct()
                .ToList();

            var summary = $"**Medical History Summary**\n\n";
            summary += $"**Period Covered:** {dateRange}\n";
            summary += $"**Total Records:** {recordCount} entries\n\n";

            if (uniqueTreatments.Any())
            {
                summary += $"**Treatment History:**\n";
                foreach (var treatment in uniqueTreatments.Take(5))
                {
                    summary += $"• {treatment}\n";
                }
                if (uniqueTreatments.Count > 5)
                    summary += $"• ... and {uniqueTreatments.Count - 5} other treatments\n";
                summary += "\n";
            }

            if (knownAllergies.Any())
            {
                summary += $"**Known Allergies:** {string.Join(", ", knownAllergies)}\n\n";
            }

            if (currentMedications.Any())
            {
                summary += $"**Medications:** {string.Join(", ", currentMedications)}\n\n";
            }

            summary += $"**Key Observations:**\n";
            summary += $"• This patient has been receiving dental care over the period from {dateRange}\n";
            summary += $"• Medical records indicate {recordCount} documented visits/treatments\n";
            
            if (knownAllergies.Any())
                summary += $"• Patient has documented allergies that should be considered for future treatments\n";
            
            if (currentMedications.Any())
                summary += $"• Current medications should be reviewed for drug interactions\n";

            summary += $"\n*This summary was automatically generated from {recordCount} patient records. Please review individual records for complete details.*";

            return summary;
        }

        private async Task<bool> IsAzureSqlDatabaseAsync()
        {
            try
            {
                // Check if we're running on Azure SQL Database by looking for Azure-specific features
                var connectionString = _context.Database.GetConnectionString();
                
                if (string.IsNullOrEmpty(connectionString))
                    return false;

                // Check if connection string contains Azure SQL Database indicators - no DB call needed
                if (connectionString.Contains(".database.windows.net") || 
                    connectionString.Contains("database.azure.com"))
                {
                    return true;
                }

                // Only check stored procedure if connection string check is inconclusive
                // Use Entity Framework's connection management instead of manual SqlConnection
                var result = await _context.Database.SqlQueryRaw<int?>(
                    "SELECT OBJECT_ID('sp_invoke_external_rest_endpoint')").FirstOrDefaultAsync();
                
                // If sp_invoke_external_rest_endpoint exists, we're likely on Azure SQL Database
                return result.HasValue && result.Value > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting Azure SQL Database, defaulting to false");
                return false;
            }
        }

        private async Task<string> CallAzureOpenAIFromSqlAsync(string medicalHistoryText)
        {
            try
            {
                // Get OpenAI configuration from appsettings
                var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
                var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
                
                if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(deploymentName))
                {
                    _logger.LogWarning("Azure OpenAI configuration missing. Endpoint: {Endpoint}, Deployment: {Deployment}", 
                        openAiEndpoint, deploymentName);
                    return string.Empty;
                }

                // Create the comprehensive prompt for medical summarization
                var medicalPrompt = @"You are a medical AI assistant specialized in dental care. Create a concise, professional medical history summary for a dental patient based on their medical records.

Focus on:
1. **Treatment History**: List key dental treatments and procedures
2. **Medical Conditions**: Relevant medical history affecting dental care  
3. **Allergies & Medications**: Important for treatment planning
4. **Key Clinical Observations**: Patterns or recurring issues
5. **Recommendations**: Brief clinical insights for future care

Format the response in markdown with clear sections. Keep it professional and concise (max 500 words). Focus on clinically relevant information that would help other dental professionals understand the patient's history quickly.

Patient Medical Records to Summarize:
" + medicalHistoryText + @"

Please create a professional medical summary that would be useful for dental healthcare providers.";

                // Use Entity Framework's connection management instead of manual SqlConnection
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"
                    DECLARE @response NVARCHAR(MAX);
                    DECLARE @result INT;
                    
                    EXEC @result = sp_CallAzureOpenAI 
                        @endpoint = @OpenAIEndpoint,
                        @deployment_name = @DeploymentName,
                        @user_message = @UserPrompt,
                        @max_tokens = 600,
                        @temperature = 0.3,
                        @response = @response OUTPUT;
                    
                    SELECT @result AS ReturnCode, dbo.fn_ExtractOpenAIContent(@response) AS AIResponse;
                ";

                var endpointParam = new SqlParameter("@OpenAIEndpoint", openAiEndpoint);
                var deploymentParam = new SqlParameter("@DeploymentName", deploymentName);
                var promptParam = new SqlParameter("@UserPrompt", medicalPrompt);
                
                command.Parameters.Add(endpointParam);
                command.Parameters.Add(deploymentParam);
                command.Parameters.Add(promptParam);

                // Ensure connection is opened through Entity Framework
                await _context.Database.OpenConnectionAsync();
                try
                {
                    using var reader = await command.ExecuteReaderAsync();
                    
                    if (await reader.ReadAsync())
                    {
                        var returnCode = reader.GetInt32(0);  // ReturnCode is first column
                        var aiResponse = reader.IsDBNull(1) ? null : reader.GetString(1);  // AIResponse is second column
                        
                        if (returnCode == 0 && !string.IsNullOrEmpty(aiResponse))
                        {
                            _logger.LogInformation("Successfully generated AI summary via Azure SQL Database");
                            return aiResponse;
                        }
                        else
                        {
                            _logger.LogWarning("Azure OpenAI call failed with return code: {ReturnCode}", returnCode);
                        }
                    }
                }
                finally
                {
                    // Ensure connection is properly closed
                    await _context.Database.CloseConnectionAsync();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Azure OpenAI from SQL Database");
                return string.Empty;
            }
        }
    }
}