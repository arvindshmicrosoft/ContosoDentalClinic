using DentalClinicWebApp.Data;
using DentalClinicWebApp.Models;
using DentalClinicWebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinicWebApp.Controllers
{
    [Authorize(Roles = "Doctor,DentalHygienist,Administrator,FrontOffice")]
    public class PatientRecordsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMedicalHistorySummarizationService _summarizationService;

        public PatientRecordsController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            IMedicalHistorySummarizationService summarizationService)
        {
            _context = context;
            _userManager = userManager;
            _summarizationService = summarizationService;
        }

        // GET: PatientRecords
        public async Task<IActionResult> Index(string searchString, string patientId)
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUser = currentUserId != null ? await _userManager.FindByIdAsync(currentUserId) : null;
            var isAdminOrFrontOffice = User.IsInRole("Administrator") || User.IsInRole("FrontOffice");

            var query = _context.PatientRecords
                .Include(pr => pr.Patient)
                .Include(pr => pr.Doctor)
                .AsQueryable();

            // Filter by patient if specified
            if (!string.IsNullOrEmpty(patientId))
            {
                query = query.Where(pr => pr.PatientId == patientId);
            }

            // Search functionality
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(pr => 
                    (pr.Patient.FirstName ?? "").Contains(searchString) ||
                    (pr.Patient.LastName ?? "").Contains(searchString) ||
                    (pr.Patient.Email ?? "").Contains(searchString) ||
                    (pr.MedicalHistory ?? "").Contains(searchString) ||
                    (pr.Notes ?? "").Contains(searchString));
            }

            // INTENTIONAL: All staff with patient record access (Doctor, DentalHygienist, FrontOffice) 
            // can view all patient records. This is by design for this demo application to allow
            // comprehensive care coordination. In a production HIPAA-compliant system, you would
            // typically restrict doctors to only their assigned patients by uncommenting the code below:
            // if (User.IsInRole("Doctor") && !isAdminOrFrontOffice)
            // {
            //     query = query.Where(pr => pr.DoctorId == currentUserId);
            // }

            var records = await query
                .OrderByDescending(pr => pr.RecordDate)
                .ToListAsync();

            // Get all patients for the dropdown
            var allPatients = await _userManager.GetUsersInRoleAsync("Patient");
            
            ViewBag.SearchString = searchString;
            ViewBag.PatientId = patientId;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.IsAdminOrFrontOffice = isAdminOrFrontOffice;
            ViewBag.Patients = allPatients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName);

            return View(records);
        }

        // GET: PatientRecords/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var patientRecord = await _context.PatientRecords
                .Include(pr => pr.Patient)
                .Include(pr => pr.Doctor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (patientRecord == null)
            {
                return NotFound();
            }

            // Check authorization
            var currentUserId = _userManager.GetUserId(User);
            var isAuthorized = User.IsInRole("Administrator") || 
                             User.IsInRole("FrontOffice") ||
                             User.IsInRole("Doctor") ||
                             User.IsInRole("DentalHygienist") ||
                             patientRecord.DoctorId == currentUserId ||
                             patientRecord.PatientId == currentUserId;

            if (!isAuthorized)
            {
                return Forbid();
            }

            return View(patientRecord);
        }

        // GET: PatientRecords/Create
        [Authorize(Roles = "Doctor,DentalHygienist,Administrator,FrontOffice")]
        public async Task<IActionResult> Create(string patientId)
        {
            var patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.Patients = patients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName);

            var doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            ViewBag.Doctors = doctors.OrderBy(d => d.LastName).ThenBy(d => d.FirstName);

            var currentUserId = _userManager.GetUserId(User);
            
            var model = new PatientRecord
            {
                DoctorId = currentUserId ?? "",
                RecordDate = DateTime.Today,
                IsActive = true
            };

            if (!string.IsNullOrEmpty(patientId))
            {
                model.PatientId = patientId;
            }

            return View(model);
        }

        // POST: PatientRecords/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor,DentalHygienist,Administrator,FrontOffice")]
        public async Task<IActionResult> Create([Bind("PatientId,DoctorId,Title,MedicalHistory,Allergies,Medications,TreatmentPlan,Notes,RecordDate,IsActive")] PatientRecord patientRecord)
        {
            // Remove validation errors for navigation properties
            ModelState.Remove("Patient");
            ModelState.Remove("Doctor");

            if (ModelState.IsValid)
            {
                // Verify the patient exists and has Patient role
                var patient = await _userManager.FindByIdAsync(patientRecord.PatientId);
                if (patient == null || !await _userManager.IsInRoleAsync(patient, "Patient"))
                {
                    ModelState.AddModelError("PatientId", "Invalid patient selected.");
                    await PopulateDropdowns();
                    return View(patientRecord);
                }

                // Verify the doctor exists and has appropriate role
                var doctor = await _userManager.FindByIdAsync(patientRecord.DoctorId);
                if (doctor == null || (!await _userManager.IsInRoleAsync(doctor, "Doctor") && 
                                     !await _userManager.IsInRoleAsync(doctor, "DentalHygienist")))
                {
                    ModelState.AddModelError("DoctorId", "Invalid doctor selected.");
                    await PopulateDropdowns();
                    return View(patientRecord);
                }

                _context.Add(patientRecord);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Medical record created successfully.";
                return RedirectToAction(nameof(Details), new { id = patientRecord.Id });
            }

            await PopulateDropdowns();
            return View(patientRecord);
        }

        // GET: PatientRecords/Edit/5
        [Authorize(Roles = "Doctor,DentalHygienist,Administrator,FrontOffice")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var patientRecord = await _context.PatientRecords.FindAsync(id);
            if (patientRecord == null)
            {
                return NotFound();
            }

            // Check authorization - only the creating doctor or admin can edit
            var currentUserId = _userManager.GetUserId(User);
            var isAuthorized = User.IsInRole("Administrator") || patientRecord.DoctorId == currentUserId;

            if (!isAuthorized)
            {
                return Forbid();
            }

            await PopulateDropdowns();
            return View(patientRecord);
        }

        // POST: PatientRecords/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor,DentalHygienist,Administrator,FrontOffice")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PatientId,DoctorId,Title,MedicalHistory,Allergies,Medications,TreatmentPlan,Notes,RecordDate,IsActive")] PatientRecord patientRecord)
        {
            if (id != patientRecord.Id)
            {
                return NotFound();
            }

            // Check authorization
            var originalRecord = await _context.PatientRecords.AsNoTracking().FirstOrDefaultAsync(pr => pr.Id == id);
            if (originalRecord == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var isAuthorized = User.IsInRole("Administrator") || originalRecord.DoctorId == currentUserId;

            if (!isAuthorized)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(patientRecord);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Medical record updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = patientRecord.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PatientRecordExists(patientRecord.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            await PopulateDropdowns();
            return View(patientRecord);
        }

        // GET: PatientRecords/PatientHistory/patientId
        public async Task<IActionResult> PatientHistory(string patientId)
        {
            if (string.IsNullOrEmpty(patientId))
            {
                return NotFound();
            }

            var patient = await _userManager.FindByIdAsync(patientId);
            if (patient == null || !await _userManager.IsInRoleAsync(patient, "Patient"))
            {
                return NotFound();
            }

            // Check authorization
            var currentUserId = _userManager.GetUserId(User);
            var isAuthorized = User.IsInRole("Administrator") || 
                             User.IsInRole("FrontOffice") ||
                             User.IsInRole("Doctor") ||
                             User.IsInRole("DentalHygienist") ||
                             patientId == currentUserId;

            if (!isAuthorized)
            {
                return Forbid();
            }

            var records = await _context.PatientRecords
                .Where(pr => pr.PatientId == patientId && pr.IsActive)
                .Include(pr => pr.Doctor)
                .OrderByDescending(pr => pr.RecordDate)
                .ToListAsync();

            var appointments = await _context.Appointments
                .Where(a => a.PatientId == patientId)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDateTime)
                .ToListAsync();

            // Get or create medical history summary
            PatientMedicalHistorySummary? medicalSummary = null;
            if (records.Any())
            {
                try
                {
                    medicalSummary = await _summarizationService.GetOrCreateSummaryAsync(patientId, currentUserId!);
                }
                catch (Exception)
                {
                    // Log the error but don't fail the page load
                    // The view will handle the case where summary is null
                    ViewBag.SummaryError = "Unable to generate medical history summary at this time.";
                }
            }

            var viewModel = new PatientHistoryViewModel
            {
                Patient = patient,
                MedicalRecords = records,
                Appointments = appointments,
                MedicalHistorySummary = medicalSummary
            };

            return View(viewModel);
        }

        // POST: PatientRecords/AddQuickNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor,DentalHygienist,Administrator,FrontOffice")]
        public async Task<IActionResult> AddQuickNote(string patientId, string notes)
        {
            if (string.IsNullOrEmpty(patientId) || string.IsNullOrEmpty(notes))
            {
                return Json(new { success = false, message = "Patient ID and notes are required." });
            }

            var patient = await _userManager.FindByIdAsync(patientId);
            if (patient == null || !await _userManager.IsInRoleAsync(patient, "Patient"))
            {
                return Json(new { success = false, message = "Invalid patient." });
            }

            var currentUserId = _userManager.GetUserId(User);
            
            var quickNote = new PatientRecord
            {
                PatientId = patientId,
                DoctorId = currentUserId ?? "",
                Notes = notes,
                RecordDate = DateTime.Today,
                IsActive = true
            };

            _context.PatientRecords.Add(quickNote);
            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = "Quick note added successfully.",
                recordId = quickNote.Id
            });
        }

        // Helper method to populate dropdowns
        private async Task PopulateDropdowns()
        {
            var patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.Patients = patients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName);

            var doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            var hygienists = await _userManager.GetUsersInRoleAsync("DentalHygienist");
            var allProviders = doctors.Concat(hygienists).OrderBy(d => d.LastName).ThenBy(d => d.FirstName);
            ViewBag.Doctors = allProviders;
        }

        private bool PatientRecordExists(int id)
        {
            return _context.PatientRecords.Any(e => e.Id == id);
        }
    }
}