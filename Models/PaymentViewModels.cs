using System.ComponentModel.DataAnnotations;
using DentalClinicWebApp.Attributes;

namespace DentalClinicWebApp.Models
{
    public class PaymentRequestViewModel
    {
        public int InvoiceId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than 0")]
        [Display(Name = "Payment Amount")]
        public decimal Amount { get; set; }
        
        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;
        
        // Credit Card Payment Details
        public CreditCardPaymentViewModel? CreditCard { get; set; }
        
        // Check Payment Details
        public CheckPaymentViewModel? Check { get; set; }
        
        // Bank Account Payment Details
        public BankAccountPaymentViewModel? BankAccount { get; set; }
        
        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }

    public class CreditCardPaymentViewModel
    {
        [Required(ErrorMessage = "Card number is required")]
        [StringLength(19, ErrorMessage = "Invalid card number length")]
        [Display(Name = "Card Number")]
        [RegularExpression(@"^(?:\d{4}\s?){3}\d{1,4}$", ErrorMessage = "Invalid credit card number format")]
        [CreditCardValidation(ErrorMessage = "Please enter a valid credit card number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Cardholder name is required")]
        [StringLength(100, ErrorMessage = "Cardholder name cannot exceed 100 characters")]
        [Display(Name = "Cardholder Name")]
        public string CardholderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expiration date is required")]
        [StringLength(7, ErrorMessage = "Invalid expiration date format")]
        [Display(Name = "Expiration Date (MM/YYYY)")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/20[2-9][0-9]$", ErrorMessage = "Invalid expiration date format (MM/YYYY)")]
        public string ExpirationDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "CVV is required")]
        [StringLength(4, ErrorMessage = "CVV must be 3 or 4 digits")]
        [MinLength(3, ErrorMessage = "CVV must be 3 or 4 digits")]
        [Display(Name = "CVV")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits")]
        public string CVV { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Billing address cannot exceed 200 characters")]
        [Display(Name = "Billing Address")]
        public string? BillingAddress { get; set; }

        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
        [Display(Name = "City")]
        public string? BillingCity { get; set; }

        [StringLength(20, ErrorMessage = "State/Province cannot exceed 20 characters")]
        [Display(Name = "State/Province")]
        public string? BillingState { get; set; }

        [StringLength(20, ErrorMessage = "ZIP/Postal code cannot exceed 20 characters")]
        [Display(Name = "ZIP/Postal Code")]
        public string? BillingZipCode { get; set; }
    }

    public class CheckPaymentViewModel
    {
        [Required(ErrorMessage = "Check number is required")]
        [StringLength(20, ErrorMessage = "Check number cannot exceed 20 characters")]
        [Display(Name = "Check Number")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Check number must contain only digits")]
        public string CheckNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Routing number is required")]
        [StringLength(9, MinimumLength = 9, ErrorMessage = "Routing number must be exactly 9 digits")]
        [Display(Name = "Bank Routing Number")]
        [RegularExpression(@"^[0-9]{9}$", ErrorMessage = "Routing number must be exactly 9 digits")]
        public string RoutingNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account number is required")]
        [StringLength(20, ErrorMessage = "Account number cannot exceed 20 characters")]
        [Display(Name = "Account Number")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Account number must contain only digits")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank name is required")]
        [StringLength(100, ErrorMessage = "Bank name cannot exceed 100 characters")]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Memo cannot exceed 200 characters")]
        [Display(Name = "Memo")]
        public string? Memo { get; set; }
    }

    public class BankAccountPaymentViewModel
    {
        [Required(ErrorMessage = "ACH routing number is required")]
        [StringLength(9, MinimumLength = 9, ErrorMessage = "ACH routing number must be exactly 9 digits")]
        [Display(Name = "ACH Routing Number")]
        [RegularExpression(@"^[0-9]{9}$", ErrorMessage = "ACH routing number must be exactly 9 digits")]
        public string RoutingNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account number is required")]
        [StringLength(20, ErrorMessage = "Account number cannot exceed 20 characters")]
        [Display(Name = "Account Number")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Account number must contain only digits")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account holder name is required")]
        [StringLength(100, ErrorMessage = "Account holder name cannot exceed 100 characters")]
        [Display(Name = "Account Holder Name")]
        public string AccountHolderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account type is required")]
        [Display(Name = "Account Type")]
        public BankAccountType AccountType { get; set; }

        [Required(ErrorMessage = "Bank name is required")]
        [StringLength(100, ErrorMessage = "Bank name cannot exceed 100 characters")]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Notes cannot exceed 200 characters")]
        [Display(Name = "Additional Notes")]
        public string? Notes { get; set; }
    }
}