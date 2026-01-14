using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DentalClinicWebApp.Models
{
    public class StaffAssignment
    {
        public int Id { get; set; }

        [Required]
        public string StaffId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "decimal(10,2)")]
        public decimal? HourlyRate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("StaffId")]
        public virtual ApplicationUser Staff { get; set; } = null!;

        public virtual ICollection<ShiftSchedule> ShiftSchedules { get; set; } = new List<ShiftSchedule>();
    }

    public class ShiftSchedule
    {
        public int Id { get; set; }

        public int StaffAssignmentId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual StaffAssignment StaffAssignment { get; set; } = null!;
    }

    public class PatientRecord
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        [Required]
        public string DoctorId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string MedicalHistory { get; set; } = string.Empty;

        public string? Allergies { get; set; }

        public string? Medications { get; set; }

        public string? TreatmentPlan { get; set; }

        public string? Notes { get; set; }

        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastModifiedDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("PatientId")]
        [ValidateNever]
        public virtual ApplicationUser Patient { get; set; } = null!;

        [ForeignKey("DoctorId")]
        [ValidateNever]
        public virtual ApplicationUser Doctor { get; set; } = null!;
    }

    public class PatientMedicalHistorySummary
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        [Required]
        public string AiGeneratedSummary { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime LastUpdatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        [StringLength(50)]
        public string ModelUsed { get; set; } = "gpt-4"; // Track which AI model generated the summary

        public int SourceRecordsCount { get; set; } // Number of patient records used for summarization

        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("PatientId")]
        [ValidateNever]
        public virtual ApplicationUser Patient { get; set; } = null!;

        [ForeignKey("CreatedByUserId")]
        [ValidateNever]
        public virtual ApplicationUser CreatedBy { get; set; } = null!;
    }

    public class InsuranceRequest
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string InsuranceProvider { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string PolicyNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string RequestNumber { get; set; } = string.Empty;

        public int ServiceId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal RequestedAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ApprovedAmount { get; set; }

        public InsuranceRequestStatus Status { get; set; } = InsuranceRequestStatus.Submitted;

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        public DateTime? ResponseDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(500)]
        public string? ResponseNotes { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("PatientId")]
        public virtual ApplicationUser Patient { get; set; } = null!;

        public virtual Service Service { get; set; } = null!;

        [ForeignKey("CreatedById")]
        public virtual ApplicationUser CreatedBy { get; set; } = null!;
    }

    public enum InsuranceRequestStatus
    {
        Submitted,
        UnderReview,
        Approved,
        PartiallyApproved,
        Denied,
        Cancelled
    }

    // ViewModels for Patient Records
    public class PatientHistoryViewModel
    {
        public ApplicationUser Patient { get; set; } = null!;
        public IEnumerable<PatientRecord> MedicalRecords { get; set; } = new List<PatientRecord>();
        public IEnumerable<Appointment> Appointments { get; set; } = new List<Appointment>();
        public PatientMedicalHistorySummary? MedicalHistorySummary { get; set; }
    }
}