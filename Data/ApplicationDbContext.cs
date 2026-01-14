using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DentalClinicWebApp.Models;

namespace DentalClinicWebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets for all entities
        public DbSet<Service> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<PatientReport> PatientReports { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<CreditCardPayment> CreditCardPayments { get; set; }
        public DbSet<CheckPayment> CheckPayments { get; set; }
        public DbSet<BankAccountPayment> BankAccountPayments { get; set; }
        public DbSet<StaffAssignment> StaffAssignments { get; set; }
        public DbSet<ShiftSchedule> ShiftSchedules { get; set; }
        public DbSet<PatientRecord> PatientRecords { get; set; }
        public DbSet<PatientMedicalHistorySummary> PatientMedicalHistorySummaries { get; set; }
        public DbSet<InsuranceRequest> InsuranceRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships and constraints
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(u => u.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PatientReport>()
                .HasOne(pr => pr.Patient)
                .WithMany(u => u.PatientReports)
                .HasForeignKey(pr => pr.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PatientReport>()
                .HasOne(pr => pr.CreatedBy)
                .WithMany()
                .HasForeignKey(pr => pr.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Patient)
                .WithMany(u => u.Invoices)
                .HasForeignKey(i => i.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StaffAssignment>()
                .HasOne(sa => sa.Staff)
                .WithMany(u => u.StaffAssignments)
                .HasForeignKey(sa => sa.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PatientRecord>()
                .HasOne(pr => pr.Patient)
                .WithMany(u => u.PatientRecords)
                .HasForeignKey(pr => pr.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PatientRecord>()
                .HasOne(pr => pr.Doctor)
                .WithMany()
                .HasForeignKey(pr => pr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PatientMedicalHistorySummary>()
                .HasOne(pmhs => pmhs.Patient)
                .WithMany()
                .HasForeignKey(pmhs => pmhs.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PatientMedicalHistorySummary>()
                .HasOne(pmhs => pmhs.CreatedBy)
                .WithMany()
                .HasForeignKey(pmhs => pmhs.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure only one summary per patient
            modelBuilder.Entity<PatientMedicalHistorySummary>()
                .HasIndex(pmhs => pmhs.PatientId)
                .IsUnique();

            modelBuilder.Entity<InsuranceRequest>()
                .HasOne(ir => ir.Patient)
                .WithMany()
                .HasForeignKey(ir => ir.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InsuranceRequest>()
                .HasOne(ir => ir.CreatedBy)
                .WithMany()
                .HasForeignKey(ir => ir.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure payment detail relationships (one-to-one with Payment)
            modelBuilder.Entity<CreditCardPayment>()
                .HasOne(cc => cc.Payment)
                .WithOne(p => p.CreditCardPayment)
                .HasForeignKey<CreditCardPayment>(cc => cc.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CheckPayment>()
                .HasOne(c => c.Payment)
                .WithOne(p => p.CheckPayment)
                .HasForeignKey<CheckPayment>(c => c.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BankAccountPayment>()
                .HasOne(ba => ba.Payment)
                .WithOne(p => p.BankAccountPayment)
                .HasForeignKey<BankAccountPayment>(ba => ba.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Static date for seed data to avoid dynamic value issues
            var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            // Seed Services
            modelBuilder.Entity<Service>().HasData(
                new Service { Id = 1, Name = "Doctor Exam w/Full-mouth X-rays (FMX)", Description = "Comprehensive dental examination with full mouth X-rays", Price = 250.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 2, Name = "Adult prophylaxis / Deep Cleaning", Description = "Professional teeth cleaning for adults", Price = 150.00m, DurationMinutes = 120, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 3, Name = "Child prophylaxis", Description = "Professional teeth cleaning for children", Price = 100.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 4, Name = "Removable partial - metal frame", Description = "Removable partial denture with metal framework", Price = 1200.00m, DurationMinutes = 120, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 5, Name = "Porcelain Crown", Description = "High-quality porcelain dental crown", Price = 1500.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 6, Name = "Amalgam / 1 surface", Description = "Single surface amalgam filling", Price = 180.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 7, Name = "Anterior Composite / 1 surface", Description = "Single surface composite filling for front teeth", Price = 220.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 8, Name = "Posterior Composite / 1 surface", Description = "Single surface composite filling for back teeth", Price = 200.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate },
                new Service { Id = 9, Name = "Night Guard", Description = "Custom night guard for teeth grinding protection", Price = 400.00m, DurationMinutes = 60, IsActive = true, CreatedDate = seedDate }
            );
        }
    }
}