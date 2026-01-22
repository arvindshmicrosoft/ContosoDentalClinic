# Contoso University Dental Clinic

A comprehensive web application for managing a university dental clinic, built with ASP.NET Core 9.0 and Entity Framework Core.

---

## Features

### Patient Features
- **Appointment Management**: Schedule, reschedule, and cancel dental appointments
- **Service Browsing**: View available dental services with pricing and descriptions
- **Invoice Management**: View invoices and make online payments
- **Payment Processing**: Multiple payment methods (Credit Card, Check, Bank Account)
- **Treatment History**: Access past visit reports and treatment records

### Staff Features
- **Front Office Staff**: Manage patient appointments, handle payment processing, invoice management
- **Doctors & Dental Hygienists**: View assigned appointments, access and manage patient medical records
- **Clinic Administrators**: Full system administration, user/role management, service catalog

### Medical Records
- Patient medical history management
- AI-powered medical history summarization (Azure OpenAI integration)
- Treatment reports and visit documentation

### Dental Services Offered
- Doctor Exam w/Full-mouth X-rays (FMX)
- Adult prophylaxis / Deep Cleaning
- Child prophylaxis
- Removable partial - metal frame
- Porcelain Crown
- Amalgam / 1 surface
- Anterior & Posterior Composite / 1 surface
- Night Guard

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET Core 9.0 MVC |
| Database | SQL Server / Azure SQL Database |
| ORM | Entity Framework Core 9.0 |
| Authentication | ASP.NET Core Identity |
| Frontend | Bootstrap 5, Font Awesome |
| AI Integration | Azure OpenAI (optional) |

---

## Quick Start

### Prerequisites
- .NET 9.0 SDK or later
- SQL Server or SQL Server LocalDB
- Visual Studio Code (recommended) or Visual Studio
- Entity Framework Core tools (install globally):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### Installation

```bash
# 1. Navigate to the project directory (where DentalClinicWebApp.csproj is located)
cd "path\to\Contoso University Dental Clinic"

# 2. Restore NuGet packages
dotnet restore DentalClinicWebApp.csproj

# 3. Build the project (required before running migrations)
dotnet build DentalClinicWebApp.csproj

# 4. Configure passwords in appsettings.json (see Configuration section)

# 5. Create and update the database
dotnet ef database update

# 6. Run the application
dotnet run --project DentalClinicWebApp.csproj
```

Access the application at `http://localhost:5081`

---

## Configuration

### Required: Update appsettings.json

Before running, configure the following in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DentalClinicDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "DefaultAdmin": {
    "Email": "admin@contosodentalclinic.com",
    "Password": "<YOUR_ADMIN_PASSWORD>"
  },
  "DefaultDoctor": {
    "Password": "<YOUR_DOCTOR_PASSWORD>"
  },
  "DefaultPatient": {
    "Email": "patient1@example.com",
    "Password": "<YOUR_PATIENT_PASSWORD>"
  }
}
```

> ⚠️ **Important**: Replace all `<YOUR_*_PASSWORD>` placeholders with strong, unique passwords before deploying.

### Azure Deployment

For Azure deployments, update `appsettings.Production.json` and use the deployment scripts in `scripts/powershell/`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:<your-server>.database.windows.net,1433;Initial Catalog=<your-database>;Authentication=Active Directory Managed Identity;User Id=<your-managed-identity-client-id>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

See [Azure Deployment Guide](docs/AZURE_DEPLOYMENT_GUIDE.md) for complete instructions.

---

## User Roles

| Role | Permissions |
|------|------------|
| **Patient** | Schedule appointments, view invoices, make payments |
| **Front Office** | Manage all appointments, handle billing |
| **Doctor** | View assigned appointments, manage patient records |
| **Dental Hygienist** | View assigned appointments, manage patient records |
| **Administrator** | Full system access and configuration |

---

## Database Schema

The application uses Entity Framework Code First with these main entities:

| Entity | Description |
|--------|-------------|
| `ApplicationUser` | Extended Identity user with dental clinic fields |
| `Service` | Dental services with pricing and duration |
| `Appointment` | Patient appointments with doctors |
| `Invoice` / `InvoiceItem` | Billing and invoicing system |
| `Payment` | Payment tracking and processing |
| `PatientRecord` | Medical records and treatment history |
| `StaffAssignment` | Staff roles and shift scheduling |
| `InsuranceRequest` | Insurance claims and requests |

---

## Development

### Running in VS Code

1. Open the project folder in VS Code
2. Install the C# extension if not already installed
3. Press `Ctrl+Shift+P` → ">.NET: Generate Assets for Build and Debug"
4. Press `F5` to run with debugging

### Database Migrations

```bash
# Create a new migration after model changes
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

### Project Structure

```
├── Controllers/        # MVC Controllers
├── Views/              # Razor Views
├── Models/             # Data models and view models
├── Data/               # Entity Framework DbContext
├── Services/           # Business logic services
├── Migrations/         # EF Core database migrations
├── scripts/
│   ├── powershell/     # Deployment and configuration scripts
│   └── sql/            # Database setup and utility scripts
├── docs/               # Documentation
└── wwwroot/            # Static files (CSS, JS, images)
```

---

## Security

- All forms include anti-forgery tokens
- Role-based authorization at controller and action level
- Admin protection: default admin cannot lose administrator privileges
- Password policies configurable in Program.cs
- Sensitive payment data masking
- HIPAA-compliant privacy policy

---

## Cost Optimization

- **Development**: SQL Server LocalDB (free)
- **Production**: Azure SQL Database serverless tier (auto-pause, pay-per-use)
- **App Hosting**: Azure App Service F1 free tier for testing
- Efficient EF Core queries with proper relationships
- Minimal external dependencies

---

## Documentation

| Document | Description |
|----------|-------------|
| [Functional Specification](docs/FUNCTIONAL_SPECIFICATION.md) | User stories, scheduling rules, payment specs |
| [Azure Deployment Guide](docs/AZURE_DEPLOYMENT_GUIDE.md) | Azure hosting instructions |
| [Medical Records System](docs/MEDICAL_RECORDS.md) | Medical records & AI summarization |
| [Azure OpenAI Integration](docs/AZURE_OPENAI_INTEGRATION.md) | AI feature setup |
| [Debugging Guide](docs/DEBUGGING_LESSONS_LEARNED.md) | Troubleshooting tips |

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Database connection errors | Verify connection string and SQL Server is running |
| Migration errors | Run `dotnet ef database update` |
| Package restore failures | Run `dotnet restore` |
| Port conflicts | Check `Properties/launchSettings.json` for port configuration |

For detailed troubleshooting, see [Debugging Guide](docs/DEBUGGING_LESSONS_LEARNED.md).

---

## Disclaimer

This is a **fictional demonstration application**. All names, email addresses (including `@contosodentalclinic.com`), patient records, and other data are entirely fictitious and used for educational purposes only. The domain `contosodentalclinic.com` does not exist and is not affiliated with any real organization.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
