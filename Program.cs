using DentalClinicWebApp.Data;
using DentalClinicWebApp.Models;
using DentalClinicWebApp.Services;
using DentalClinicWebApp.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Configure logging for Azure
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add Entity Framework connection logging for monitoring
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // Configure SQL Server with appropriate authentication
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
            
        // Set explicit command timeout to prevent long-running queries
        sqlOptions.CommandTimeout(30);  // 30 seconds timeout
        
        // Use managed identity token provider in production
        if (builder.Environment.IsProduction())
        {
            sqlOptions.CommandTimeout(60);  // Longer timeout for production (Azure latency)
        }
    });
    
    // Azure managed identity is configured via connection string
    // No additional service registration needed
    
    // Reduce logging in production and enable connection pooling optimizations
    if (builder.Environment.IsProduction())
    {
        options.EnableSensitiveDataLogging(false);
        options.EnableServiceProviderCaching();
    }
    else
    {
        // Enable detailed logging in development for debugging connection issues
        options.EnableSensitiveDataLogging(true);
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

builder.Services.AddDefaultIdentity<ApplicationUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    
    // More secure password requirements for production
    if (builder.Environment.IsProduction())
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
    }
    else
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
    }
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Register custom services
builder.Services.AddScoped<IMedicalHistorySummarizationService, MedicalHistorySummarizationService>();

// Add Application Insights for Azure monitoring (will be configured via Azure portal)
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Seed roles and admin user
await SeedRolesAndUsers(app);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

// Run credit card validation tests in development
// Temporarily disabled for debugging payment UI
//if (app.Environment.IsDevelopment())
//{
//    CreditCardValidationTest.RunTests();
//    SimpleCardTest.RunTest();
//}

app.Run();

// Method to seed roles and users
async Task SeedRolesAndUsers(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Create roles
    string[] roleNames = { "Administrator", "Doctor", "DentalHygienist", "FrontOffice", "Patient" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Create admin user - password is configurable via appsettings
    var adminEmail = configuration["DefaultAdmin:Email"] ?? "admin@contosodentalclinic.com";
    var adminPassword = configuration["DefaultAdmin:Password"] ?? throw new InvalidOperationException("DefaultAdmin:Password must be configured in appsettings.json");
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            EmailConfirmed = true,
            IsActive = true,
            Address = "123 Admin St",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = "Other",
            CreatedDate = DateTime.UtcNow
        };
        await userManager.CreateAsync(adminUser, adminPassword);
        await userManager.AddToRoleAsync(adminUser, "Administrator");
    }

    // Create sample doctors
    var doctorEmails = new[]
    {
        ("dr.smith@contosodentalclinic.com", "Dr. Sarah", "Smith"),
        ("dr.johnson@contosodentalclinic.com", "Dr. Michael", "Johnson"),
        ("dr.davis@contosodentalclinic.com", "Dr. Emily", "Davis")
    };

    foreach (var (email, firstName, lastName) in doctorEmails)
    {
        var doctor = await userManager.FindByEmailAsync(email);
        if (doctor == null)
        {
            doctor = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                IsActive = true,
                Address = "123 Medical Plaza",
                DateOfBirth = new DateTime(1975, 6, 15),
                Gender = "Other",
                CreatedDate = DateTime.UtcNow
            };
            var doctorPassword = configuration["DefaultDoctor:Password"] ?? throw new InvalidOperationException("DefaultDoctor:Password must be configured in appsettings.json");
            await userManager.CreateAsync(doctor, doctorPassword);
            await userManager.AddToRoleAsync(doctor, "Doctor");
        }
    }

    // Seed medical records
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedPatientAndMedicalRecords(context, userManager, configuration);
    }
    catch (Exception ex)
    {
        // Log error but don't stop application startup
        // In production, use ILogger instead of Console.WriteLine
        System.Diagnostics.Debug.WriteLine($"Error seeding medical records: {ex.Message}");
    }
}

// Medical records seeding method
static async Task SeedPatientAndMedicalRecords(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    // Create sample patient
    var patientEmail = configuration["DefaultPatient:Email"] ?? "patient1@example.com";
    var patientPassword = configuration["DefaultPatient:Password"] ?? throw new InvalidOperationException("DefaultPatient:Password must be configured in appsettings.json");
    var patient = await userManager.FindByEmailAsync(patientEmail);
    
    if (patient == null)
    {
        patient = new ApplicationUser
        {
            UserName = patientEmail,
            Email = patientEmail,
            FirstName = "Vaughn",
            LastName = "Price",
            EmailConfirmed = true,
            IsActive = true,
            Address = "123 Main St, Anytown, ST 12345",
            DateOfBirth = new DateTime(1978, 9, 9),
            Gender = "Male",
            PhoneNumber = "(555) 123-4567",
            CreatedDate = DateTime.UtcNow
        };
        
        await userManager.CreateAsync(patient, patientPassword);
        await userManager.AddToRoleAsync(patient, "Patient");
    }

    // Get a doctor to assign records to
    var doctors = await userManager.GetUsersInRoleAsync("Doctor");
    var assignedDoctor = doctors.FirstOrDefault();
    
    if (assignedDoctor == null)
    {
        return; // No doctors available
    }

    // Check if records already exist for this patient
    var existingRecords = await context.PatientRecords
        .Where(pr => pr.PatientId == patient.Id)
        .AnyAsync();
        
    if (existingRecords)
    {
        return; // Records already seeded
    }

    // Create sample medical records based on the medical history file
    var medicalRecords = new[]
    {
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 1, 15),
            MedicalHistory = "Hypertension (controlled with Lisinopril), No known drug allergies",
            Allergies = "No known drug allergies",
            Medications = "Lisinopril for hypertension",
            TreatmentPlan = "Root canal therapy on #19, fillings on #30 and #31",
            Notes = "Initial Consultation - New Patient. 45-year-old male complaining of dental pain in lower left quadrant. Chief Complaint: Severe pain in tooth #19 for past 3 days. Previous Dental Work: Last cleaning 2 years ago, crown on tooth #14 in 2018",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 1, 22),
            Notes = "Diagnostic Procedures. Full mouth X-rays completed. Findings: Caries on teeth #19, #30, #31. Diagnosis: Deep caries on #19 requiring root canal therapy",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 2, 5),
            Notes = "Root Canal Therapy - Tooth #19. Procedure completed successfully under local anesthesia. Post-op instructions given. Follow-up scheduled in 2 weeks",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 2, 19),
            Notes = "Follow-up Visit. Healing progressing well. No signs of infection. Crown preparation scheduled",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 3, 5),
            Notes = "Crown Preparation - Tooth #19. Impression taken for permanent crown. Temporary crown placed. Patient tolerated procedure well",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 3, 19),
            Notes = "Crown Delivery - Tooth #19. Permanent crown cemented. Occlusion adjusted. Excellent fit and patient comfort",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 4, 2),
            Notes = "Restorative Treatment. Composite fillings placed on teeth #30 and #31. Both restorations completed successfully. Patient education on oral hygiene provided",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 7, 15),
            Notes = "Routine Cleaning and Exam. Professional prophylaxis completed. No new caries detected. Gingival inflammation noted - recommend improved flossing. Next cleaning scheduled in 6 months",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2023, 10, 20),
            Notes = "Emergency Visit. Patient reports sensitivity in tooth #14 (previous crown). Clinical exam reveals no visible issues. Possible need for bite adjustment. Scheduled follow-up if symptoms persist",
            IsActive = true
        },
        new PatientRecord
        {
            PatientId = patient.Id,
            DoctorId = assignedDoctor.Id,
            RecordDate = new DateTime(2024, 1, 20),
            Notes = "Routine Cleaning and Exam. Professional prophylaxis completed. Overall oral health improved. Gingival health much better with improved home care. Continue current oral hygiene routine",
            IsActive = true
        }
    };
    
    context.PatientRecords.AddRange(medicalRecords);
    await context.SaveChangesAsync();
}
