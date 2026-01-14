# Medical Records System

## Overview

The Contoso University Dental Clinic application includes a comprehensive medical records system that allows doctors and dental hygienists to review patient medical history, add clinical notes, and leverage AI-powered summarization.

---

## Features

### Core Medical Records Functionality

- **Role-based access control** (Doctor, DentalHygienist, Administrator)
- **Full CRUD operations** for medical records
- **Patient history timeline view** with chronological display
- **Quick note addition** for fast documentation
- **Search and filter capabilities**

### AI-Powered Medical History Summarization

When deployed to Azure, the system uses Azure OpenAI to generate intelligent summaries of patient medical records:

- **Automatic summary generation** on first access
- **Cached summaries** to avoid repeated API calls
- **Graceful fallback** to mock summaries in local development
- **Medical-specific prompts** for dental care context

---

## Architecture

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `PatientRecordsController` | Controllers/ | CRUD operations, role-based access |
| `MedicalHistorySummarizationService` | Services/ | AI summarization logic |
| `PatientRecord` | Models/ | Medical record data model |
| `PatientMedicalHistorySummary` | Models/ | AI summary storage |

### Views

- **Index** - List all medical records with search/filter
- **Create** - Add new medical records
- **Details** - View detailed record with quick note functionality
- **Edit** - Edit existing medical records
- **PatientHistory** - Comprehensive patient timeline with AI summary

### Data Flow for AI Summarization

```
Patient History Page Request
    ↓
Check for Existing Summary (cached)
    ↓
If None Found:
    ↓
Gather Patient Records → Environment Detection
    ↓                           ↓
    ↓                    [Azure SQL DB] → Call Azure OpenAI
    ↓                           ↓
    ↓                    [Local SQL] → Use Mock Summary
    ↓                                         ↓
    ← ← ← ← ← ← Save Summary ← ← ← ← ← ← ← ←
    ↓
Display Summary in UI
```

---

## User Workflows

### For Doctors/Dental Hygienists

1. **Access Medical Records**: Navigate to "Medical Records" in main menu
2. **View Patient History**: Click patient history from appointments or records list
3. **Add New Record**: Create comprehensive medical record with all relevant fields
4. **Add Quick Notes**: Use quick note feature from patient details page
5. **Search/Filter**: Find specific records using search functionality
6. **View AI Summary**: See auto-generated medical history summary (Azure only)

### For Administrators

- Full access to all medical records
- Can manage any patient's records
- Can edit records created by any doctor

### For Front Office

- View-only access to medical records
- Can access patient history for scheduling purposes

---

## Azure OpenAI Integration

### How It Works

The service automatically detects the environment:

```csharp
// Azure SQL Database detected via:
// 1. Connection string contains ".database.windows.net"
// 2. sp_invoke_external_rest_endpoint stored procedure exists

if (IsAzureSqlDatabase)
    // Call Azure OpenAI via SQL stored procedure
else
    // Use intelligent mock summary for development
```

### Medical Prompt Engineering

The AI receives comprehensive prompts focused on:

1. **Treatment History**: Key dental treatments and procedures
2. **Medical Conditions**: Relevant history affecting dental care
3. **Allergies & Medications**: Important for treatment planning
4. **Key Clinical Observations**: Patterns or recurring issues
5. **Recommendations**: Clinical insights for future care

### Configuration

**Local Development** (`appsettings.json`):
```json
{
  "AzureOpenAI": {
    "Endpoint": "",
    "DeploymentName": "gpt-4"
  }
}
```

**Azure Production** (`appsettings.Production.json`):
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-openai-service.openai.azure.com",
    "DeploymentName": "gpt-4"
  }
}
```

### Prerequisites for Azure OpenAI

1. **Database Setup**:
   - Database master key exists
   - Database scoped credential configured
   - `sp_CallAzureOpenAI` stored procedure deployed

2. **Azure OpenAI Service**:
   - Service deployed with GPT-4 model
   - Managed Identity has "Cognitive Services OpenAI User" role

---

## Data Model

### PatientRecord Entity

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| PatientId | string | Foreign key to patient |
| DoctorId | string | Foreign key to doctor |
| RecordDate | DateTime | Date of record |
| MedicalHistory | string | General medical history |
| Allergies | string | Known allergies |
| Medications | string | Current medications |
| TreatmentPlan | string | Planned treatments |
| Notes | string | Clinical notes |
| IsActive | bool | Soft delete flag |

### PatientMedicalHistorySummary Entity

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| PatientId | string | Foreign key (unique index) |
| AiGeneratedSummary | string | The AI summary text |
| ModelUsed | string | AI model (e.g., "gpt-4") |
| SourceRecordsCount | int | Number of records summarized |
| CreatedDate | DateTime | When generated |
| LastUpdatedDate | DateTime | Last refresh |
| IsActive | bool | Soft delete flag |

---

## Security

- **Role-based authorization** at controller and action level
- **Doctors can only edit their own records** (unless administrator)
- **Patient and Doctor IDs cannot be changed** after record creation
- **Input validation and CSRF protection**
- **No API keys in code** - uses Managed Identity
- **Audit trail** for all summary generations

---

## Sample Data

The system seeds a sample patient (Vaughn Price) with realistic medical history:

- Initial consultation and diagnosis
- Root canal therapy progression
- Crown preparation and delivery
- Restorative treatments (fillings)
- Routine cleanings and follow-ups
- Emergency visit documentation

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "No summary generated" | Check Azure OpenAI service availability and permissions |
| "Mock summary in Azure" | Verify stored procedures are deployed |
| "Database credential error" | Credential name must match OpenAI endpoint |
| "Permission denied" | Ensure Managed Identity has OpenAI User role |

### Validation SQL Commands

```sql
-- Check Azure SQL Database features
SELECT OBJECT_ID('sp_invoke_external_rest_endpoint') AS AzureSQLFeature;

-- Verify master key
SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##';

-- Check credentials
SELECT name FROM sys.database_scoped_credentials;

-- View existing summaries
SELECT PatientId, ModelUsed, SourceRecordsCount, CreatedDate 
FROM PatientMedicalHistorySummaries WHERE IsActive = 1;
```

---

## Future Enhancements

1. **Attachment support** for X-rays and images
2. **Treatment plan templates**
3. **Summary refresh** button for manual regeneration
4. **Multi-language support** for patient records
5. **Export functionality** for medical records
6. **Print-friendly format**
7. **Integration with clinical decision support systems**
