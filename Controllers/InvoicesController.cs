using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DentalClinicWebApp.Data;
using DentalClinicWebApp.Models;

namespace DentalClinicWebApp.Controllers
{
    [Authorize]
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InvoicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Invoices
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            IQueryable<Invoice> invoices = _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.Service);

            // Filter invoices based on user role
            if (userRoles.Contains("Administrator") || userRoles.Contains("FrontOffice"))
            {
                // No filtering - show all invoices
            }
            else if (userRoles.Contains("Patient"))
            {
                invoices = invoices.Where(i => i.PatientId == user!.Id);
            }
            // FrontOffice and Administrator can see all invoices

            var invoiceList = await invoices.OrderByDescending(i => i.InvoiceDate).ToListAsync();
            return View(invoiceList);
        }

        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.Service)
                .Include(i => i.Payments)
                    .ThenInclude(p => p.CreditCardPayment)
                .Include(i => i.Payments)
                    .ThenInclude(p => p.CheckPayment)
                .Include(i => i.Payments)
                    .ThenInclude(p => p.BankAccountPayment)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null) return NotFound();

            // Check if user has access to this invoice
            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Allow access for Administrator and FrontOffice roles for all invoices
            // Allow access for Patients only to their own invoices
            if (!userRoles.Contains("Administrator") && !userRoles.Contains("FrontOffice"))
            {
                if (!userRoles.Contains("Patient") || invoice.PatientId != user!.Id)
                {
                    return View("~/Views/Shared/AccessDenied.cshtml");
                }
            }

            return View(invoice);
        }

        // GET: Invoices/Create
        [Authorize(Roles = "FrontOffice,Administrator")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            
            var invoice = new Invoice
            {
                InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                InvoiceNumber = GenerateInvoiceNumber()
            };

            return View(invoice);
        }

        // POST: Invoices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "FrontOffice,Administrator")]
        public async Task<IActionResult> Create([Bind("PatientId,DueDate,Notes,InvoiceNumber,InvoiceDate")] Invoice invoice, List<int> ServiceIds, List<int> Quantities)
        {
            // Remove validation errors for navigation properties that are not being bound
            ModelState.Remove("Patient");
            ModelState.Remove("InvoiceItems");
            ModelState.Remove("Payments");

            if (ModelState.IsValid && ServiceIds != null && ServiceIds.Any())
            {
                invoice.InvoiceNumber = GenerateInvoiceNumber();
                invoice.InvoiceDate = DateTime.UtcNow;
                invoice.Status = InvoiceStatus.Pending;

                decimal subTotal = 0;
                var invoiceItems = new List<InvoiceItem>();

                for (int i = 0; i < ServiceIds.Count; i++)
                {
                    var service = await _context.Services.FindAsync(ServiceIds[i]);
                    if (service != null && Quantities != null && i < Quantities.Count && Quantities[i] > 0)
                    {
                        var item = new InvoiceItem
                        {
                            ServiceId = ServiceIds[i],
                            Quantity = Quantities[i],
                            UnitPrice = service.Price,
                            TotalPrice = service.Price * Quantities[i],
                            Description = service.Name
                        };
                        invoiceItems.Add(item);
                        subTotal += item.TotalPrice;
                    }
                }

                invoice.SubTotal = subTotal;
                invoice.TaxAmount = subTotal * 0.08m; // 8% tax rate
                invoice.TotalAmount = invoice.SubTotal + invoice.TaxAmount;
                invoice.BalanceAmount = invoice.TotalAmount;

                _context.Add(invoice);
                await _context.SaveChangesAsync();

                // Add invoice items
                foreach (var item in invoiceItems)
                {
                    item.InvoiceId = invoice.Id;
                    _context.Add(item);
                }
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            else
            {
                if (ServiceIds == null || !ServiceIds.Any())
                {
                    ModelState.AddModelError("", "Please select at least one service for the invoice.");
                }
            }

            ViewBag.Patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            return View(invoice);
        }

        // POST: Invoices/Pay/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, PaymentRequestViewModel model)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Check permissions - Allow Administrator, FrontOffice, and the patient who owns the invoice
            if (!userRoles.Contains("Administrator") && !userRoles.Contains("FrontOffice"))
            {
                if (!userRoles.Contains("Patient") || invoice.PatientId != user!.Id)
                {
                    return View("~/Views/Shared/AccessDenied.cshtml");
                }
            }

            // Validate payment amount
            if (model.Amount <= 0 || model.Amount > invoice.BalanceAmount)
            {
                TempData["ErrorMessage"] = "Invalid payment amount.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Validate payment method specific details
            if (!ValidatePaymentDetails(model))
            {
                TempData["ErrorMessage"] = "Please provide all required payment information.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                // Use execution strategy to handle SQL Server retry logic
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    
                    // Create the payment record
                    var payment = new Payment
                    {
                        InvoiceId = id,
                        Amount = model.Amount,
                        PaymentDate = DateTime.UtcNow,
                        PaymentMethod = model.PaymentMethod,
                        Status = PaymentStatus.Completed,
                        TransactionId = Guid.NewGuid().ToString("N")[..10].ToUpper(),
                        Notes = model.Notes
                    };

                    _context.Add(payment);
                    await _context.SaveChangesAsync();

                    // Save payment method specific details
                    await SavePaymentDetails(payment.Id, model);

                    // Update invoice amounts
                    invoice.PaidAmount += model.Amount;
                    invoice.BalanceAmount -= model.Amount;

                    if (invoice.BalanceAmount <= 0)
                    {
                        invoice.Status = InvoiceStatus.Paid;
                        invoice.BalanceAmount = 0;
                    }
                    else if (invoice.PaidAmount > 0)
                    {
                        invoice.Status = InvoiceStatus.PartiallyPaid;
                    }

                    _context.Update(invoice);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = $"Payment of ${model.Amount:F2} processed successfully.";
            }
            catch (Exception ex)
            {
                // Log error via proper logging infrastructure, not console
                // In production, use ILogger<InvoicesController> injected via DI
                TempData["ErrorMessage"] = $"Payment processing failed: {ex.Message}. Please check your payment details and try again.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private bool ValidatePaymentDetails(PaymentRequestViewModel model)
        {
            switch (model.PaymentMethod)
            {
                case "Credit Card":
                case "Debit Card":
                    return model.CreditCard != null && 
                           !string.IsNullOrWhiteSpace(model.CreditCard.CardNumber) &&
                           !string.IsNullOrWhiteSpace(model.CreditCard.CardholderName) &&
                           !string.IsNullOrWhiteSpace(model.CreditCard.ExpirationDate) &&
                           !string.IsNullOrWhiteSpace(model.CreditCard.CVV);

                case "Check":
                    return model.Check != null &&
                           !string.IsNullOrWhiteSpace(model.Check.CheckNumber) &&
                           !string.IsNullOrWhiteSpace(model.Check.RoutingNumber) &&
                           !string.IsNullOrWhiteSpace(model.Check.AccountNumber) &&
                           !string.IsNullOrWhiteSpace(model.Check.BankName);

                case "Bank Account":
                    return model.BankAccount != null &&
                           !string.IsNullOrWhiteSpace(model.BankAccount.RoutingNumber) &&
                           !string.IsNullOrWhiteSpace(model.BankAccount.AccountNumber) &&
                           !string.IsNullOrWhiteSpace(model.BankAccount.AccountHolderName) &&
                           !string.IsNullOrWhiteSpace(model.BankAccount.BankName);

                case "Cash":
                case "Insurance":
                default:
                    return true; // No additional validation needed
            }
        }

        private async Task SavePaymentDetails(int paymentId, PaymentRequestViewModel model)
        {
            switch (model.PaymentMethod)
            {
                case "Credit Card":
                case "Debit Card":
                    if (model.CreditCard != null)
                    {
                        var creditCardPayment = new CreditCardPayment
                        {
                            PaymentId = paymentId,
                            CardNumber = MaskCreditCardNumber(model.CreditCard.CardNumber),
                            CardholderName = model.CreditCard.CardholderName,
                            ExpirationDate = model.CreditCard.ExpirationDate,
                            CVV = "***", // Never store actual CVV
                            BillingAddress = model.CreditCard.BillingAddress,
                            BillingCity = model.CreditCard.BillingCity,
                            BillingState = model.CreditCard.BillingState,
                            BillingZipCode = model.CreditCard.BillingZipCode
                        };
                        _context.Add(creditCardPayment);
                    }
                    break;

                case "Check":
                    if (model.Check != null)
                    {
                        var checkPayment = new CheckPayment
                        {
                            PaymentId = paymentId,
                            CheckNumber = model.Check.CheckNumber,
                            RoutingNumber = model.Check.RoutingNumber,
                            AccountNumber = MaskAccountNumber(model.Check.AccountNumber),
                            BankName = model.Check.BankName,
                            Memo = model.Check.Memo
                        };
                        _context.Add(checkPayment);
                    }
                    break;

                case "Bank Account":
                    if (model.BankAccount != null)
                    {
                        var bankAccountPayment = new BankAccountPayment
                        {
                            PaymentId = paymentId,
                            RoutingNumber = model.BankAccount.RoutingNumber,
                            AccountNumber = MaskAccountNumber(model.BankAccount.AccountNumber),
                            AccountHolderName = model.BankAccount.AccountHolderName,
                            AccountType = model.BankAccount.AccountType,
                            BankName = model.BankAccount.BankName,
                            Notes = model.BankAccount.Notes
                        };
                        _context.Add(bankAccountPayment);
                    }
                    break;
            }

            await _context.SaveChangesAsync();
        }

        private string MaskCreditCardNumber(string cardNumber)
        {
            // Remove spaces and keep only the last 4 digits
            var cleaned = cardNumber.Replace(" ", "");
            if (cleaned.Length <= 4) return "****";
            return "**** **** **** " + cleaned.Substring(cleaned.Length - 4);
        }

        private string MaskAccountNumber(string accountNumber)
        {
            // Keep only the last 4 digits
            if (accountNumber.Length <= 4) return "****";
            return new string('*', accountNumber.Length - 4) + accountNumber.Substring(accountNumber.Length - 4);
        }

        private string GenerateInvoiceNumber()
        {
            var lastInvoice = _context.Invoices
                .OrderByDescending(i => i.Id)
                .FirstOrDefault();

            var nextNumber = (lastInvoice?.Id ?? 0) + 1;
            return $"INV-{DateTime.UtcNow:yyyyMM}-{nextNumber:D4}";
        }

        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.Id == id);
        }
    }
}