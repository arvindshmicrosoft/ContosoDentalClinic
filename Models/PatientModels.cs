using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinicWebApp.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
    }

    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        [Required]
        public string DoctorId { get; set; } = string.Empty;

        public int ServiceId { get; set; }

        public DateTime AppointmentDateTime { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        // Navigation properties
        [ForeignKey("PatientId")]
        public virtual ApplicationUser? Patient { get; set; }

        [ForeignKey("DoctorId")]
        public virtual ApplicationUser? Doctor { get; set; }

        public virtual Service? Service { get; set; }
    }

    public enum AppointmentStatus
    {
        Scheduled,
        Confirmed,
        InProgress,
        Completed,
        Cancelled,
        NoShow
    }

    public class PatientReport
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        public int AppointmentId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime ReportDate { get; set; } = DateTime.UtcNow;

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("PatientId")]
        public virtual ApplicationUser Patient { get; set; } = null!;

        public virtual Appointment Appointment { get; set; } = null!;

        [ForeignKey("CreatedById")]
        public virtual ApplicationUser CreatedBy { get; set; } = null!;
    }

    public class Invoice
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal BalanceAmount { get; set; }

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("PatientId")]
        public virtual ApplicationUser Patient { get; set; } = null!;

        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public enum InvoiceStatus
    {
        Pending,
        PartiallyPaid,
        Paid,
        Overdue,
        Cancelled
    }

    public class InvoiceItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }

        public int ServiceId { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPrice { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        // Navigation properties
        public virtual Invoice Invoice { get; set; } = null!;
        public virtual Service Service { get; set; } = null!;
    }

    public class Payment
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(100)]
        public string? TransactionId { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual Invoice Invoice { get; set; } = null!;
        
        // Payment method specific details (one-to-one relationships)
        public virtual CreditCardPayment? CreditCardPayment { get; set; }
        public virtual CheckPayment? CheckPayment { get; set; }
        public virtual BankAccountPayment? BankAccountPayment { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded
    }

    // Payment method specific models for storing sensitive payment information
    public class CreditCardPayment
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }

        [Required]
        [StringLength(19)] // Standard credit card number length with spaces
        [Display(Name = "Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Cardholder Name")]
        public string CardholderName { get; set; } = string.Empty;

        [Required]
        [StringLength(7)] // MM/YYYY format
        [Display(Name = "Expiration Date")]
        public string ExpirationDate { get; set; } = string.Empty;

        [Required]
        [StringLength(4)]
        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Billing Address")]
        public string? BillingAddress { get; set; }

        [StringLength(100)]
        [Display(Name = "Billing City")]
        public string? BillingCity { get; set; }

        [StringLength(20)]
        [Display(Name = "Billing State/Province")]
        public string? BillingState { get; set; }

        [StringLength(20)]
        [Display(Name = "Billing ZIP/Postal Code")]
        public string? BillingZipCode { get; set; }

        // Navigation property
        public virtual Payment Payment { get; set; } = null!;
    }

    public class CheckPayment
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Check Number")]
        public string CheckNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(9)]
        [Display(Name = "Routing Number")]
        public string RoutingNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Memo")]
        public string? Memo { get; set; }

        // Navigation property
        public virtual Payment Payment { get; set; } = null!;
    }

    public class BankAccountPayment
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }

        [Required]
        [StringLength(9)]
        [Display(Name = "ACH Routing Number")]
        public string RoutingNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Account Holder Name")]
        public string AccountHolderName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Account Type")]
        public BankAccountType AccountType { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Additional Notes")]
        public string? Notes { get; set; }

        // Navigation property
        public virtual Payment Payment { get; set; } = null!;
    }

    public enum BankAccountType
    {
        [Display(Name = "Checking Account")]
        Checking,
        [Display(Name = "Savings Account")]
        Savings,
        [Display(Name = "Business Checking")]
        BusinessChecking,
        [Display(Name = "Business Savings")]
        BusinessSavings
    }
}