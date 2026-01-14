using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DentalClinicWebApp.Data;
using DentalClinicWebApp.Models;

namespace DentalClinicWebApp.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Appointments
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            IQueryable<Appointment> appointments = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Service);

            // Filter appointments based on user role - prioritize higher privilege roles
            if (userRoles.Contains("Administrator") || userRoles.Contains("FrontOffice"))
            {
                // Administrators and FrontOffice can see all appointments
            }
            else if (userRoles.Contains("Doctor") || userRoles.Contains("DentalHygienist"))
            {
                appointments = appointments.Where(a => a.DoctorId == user!.Id);
            }
            else if (userRoles.Contains("Patient"))
            {
                appointments = appointments.Where(a => a.PatientId == user!.Id);
            }

            var appointmentList = await appointments.OrderBy(a => a.AppointmentDateTime).ToListAsync();
            ViewBag.CurrentUserId = user!.Id;
            return View(appointmentList);
        }

        // GET: Appointments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            // Check if user has access to this appointment
            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Administrator and FrontOffice can view all appointments
            if (userRoles.Contains("Administrator") || userRoles.Contains("FrontOffice"))
            {
                // Full access - no restrictions
            }
            else if (userRoles.Contains("Patient") && appointment.PatientId != user!.Id)
                return Forbid();
            else if ((userRoles.Contains("Doctor") || userRoles.Contains("DentalHygienist")) && appointment.DoctorId != user!.Id)
                return Forbid();

            // Set current user ID for view logic
            ViewBag.CurrentUserId = user!.Id;

            return View(appointment);
        }

        // GET: Appointments/Create
        public async Task<IActionResult> Create(int? serviceId)
        {
            // Check if user is authenticated
            if (!User.Identity!.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Check if user has required roles
            if (!userRoles.Any(r => r == "Patient" || r == "FrontOffice" || r == "Administrator"))
            {
                ViewBag.ErrorMessage = "You need to be assigned a Patient, Front Office, or Administrator role to create appointments. Please contact an administrator.";
                ViewBag.UserRoles = string.Join(", ", userRoles);
                return View("AccessDenied");
            }

            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");

            var appointment = new Appointment();
            if (userRoles.Contains("Patient"))
            {
                appointment.PatientId = user!.Id;
            }
            else
            {
                ViewBag.Patients = await _userManager.GetUsersInRoleAsync("Patient");
            }

            // Pre-select service if serviceId is provided
            if (serviceId.HasValue)
            {
                var selectedService = await _context.Services.FindAsync(serviceId.Value);
                if (selectedService != null && selectedService.IsActive)
                {
                    appointment.ServiceId = serviceId.Value;
                    ViewBag.PreSelectedService = selectedService;
                }
            }

            return View(appointment);
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PatientId,DoctorId,ServiceId,AppointmentDateTime,Notes")] Appointment appointment)
        {
            // Check if user is authenticated
            if (!User.Identity!.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Check if user has required roles
            if (!userRoles.Any(r => r == "Patient" || r == "FrontOffice" || r == "Administrator"))
            {
                ViewBag.ErrorMessage = "You need to be assigned a Patient, Front Office, or Administrator role to create appointments. Please contact an administrator.";
                ViewBag.UserRoles = string.Join(", ", userRoles);
                return View("AccessDenied");
            }

            // If patient is creating appointment, ensure they can only create for themselves
            if (userRoles.Contains("Patient"))
            {
                appointment.PatientId = user!.Id;
                // Remove any validation errors for PatientId since we're setting it programmatically
                ModelState.Remove("PatientId");
            }

            // Additional validation - ensure we compare times in the same timezone
            var appointmentLocal = appointment.AppointmentDateTime.Kind == DateTimeKind.Utc 
                ? appointment.AppointmentDateTime.ToLocalTime() 
                : appointment.AppointmentDateTime;
            var currentLocal = DateTime.Now;
            
            if (appointmentLocal <= currentLocal)
            {
                ModelState.AddModelError("AppointmentDateTime", "Appointment date and time must be in the future.");
            }

            // Check if the appointment is on a weekday
            if (appointment.AppointmentDateTime.DayOfWeek == DayOfWeek.Saturday || 
                appointment.AppointmentDateTime.DayOfWeek == DayOfWeek.Sunday)
            {
                ModelState.AddModelError("AppointmentDateTime", "Appointments are only available on weekdays (Monday-Friday).");
            }

            // Check if the time slot is available
            if (!await IsTimeSlotAvailable(appointment.DoctorId, appointment.ServiceId, appointment.AppointmentDateTime))
            {
                ModelState.AddModelError("AppointmentDateTime", "This time slot conflicts with an existing appointment. Please select a different time.");
            }

            if (ModelState.IsValid)
            {
                appointment.Status = AppointmentStatus.Scheduled;
                appointment.CreatedDate = DateTime.UtcNow;
                
                try
                {
                    _context.Add(appointment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Appointment scheduled successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred while saving the appointment: {ex.Message}");
                }
            }

            // Reload ViewBag data for form redisplay
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            if (!userRoles.Contains("Patient"))
            {
                ViewBag.Patients = await _userManager.GetUsersInRoleAsync("Patient");
            }

            return View(appointment);
        }

        // API endpoint to get available time slots
        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(string doctorId, DateTime date)
        {
            if (string.IsNullOrEmpty(doctorId) || date.Date <= DateTime.Today)
            {
                return Json(new { success = false, message = "Invalid doctor or date" });
            }

            // Check if the date is a weekday (Monday-Friday)
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                return Json(new { success = false, message = "Appointments are only available on weekdays (Monday-Friday)" });
            }

            // Define clinic hours (9 AM to 5 PM)
            var startHour = 9;
            var endHour = 17;
            var availableSlots = new List<object>();

            // Get existing appointments for the doctor on the selected date
            var existingAppointments = await _context.Appointments
                .Where(a => a.DoctorId == doctorId && 
                           a.AppointmentDateTime.Date == date.Date &&
                           a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.AppointmentDateTime.Hour)
                .ToListAsync();

            // Generate available time slots
            for (int hour = startHour; hour < endHour; hour++)
            {
                if (!existingAppointments.Contains(hour))
                {
                    var timeSlot = new DateTime(date.Year, date.Month, date.Day, hour, 0, 0);
                    availableSlots.Add(new
                    {
                        value = timeSlot.ToString("yyyy-MM-ddTHH:mm:ss"),
                        text = timeSlot.ToString("h:mm tt")
                    });
                }
            }

            return Json(new { success = true, slots = availableSlots });
        }

        // GET: Appointments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            // Check permissions
            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Check if user can edit this appointment
            if (!userRoles.Contains("Administrator") && !userRoles.Contains("FrontOffice"))
            {
                // Patients can only edit their own appointments
                if (userRoles.Contains("Patient") && appointment.PatientId != user!.Id)
                {
                    return Forbid();
                }
                // Doctors can only edit appointments assigned to them
                else if ((userRoles.Contains("Doctor") || userRoles.Contains("DentalHygienist")) && appointment.DoctorId != user!.Id)
                {
                    return Forbid();
                }
            }

            // Prevent editing cancelled appointments
            if (appointment.Status == AppointmentStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Cannot edit a cancelled appointment.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            ViewBag.Patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.CurrentUserRoles = userRoles;

            return View(appointment);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PatientId,DoctorId,ServiceId,AppointmentDateTime,Status,Notes")] Appointment appointment)
        {
            if (id != appointment.Id) return NotFound();

            // Check if the original appointment is cancelled
            var originalAppointment = await _context.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (originalAppointment == null) return NotFound();
            
            if (originalAppointment.Status == AppointmentStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Cannot edit a cancelled appointment.";
                return RedirectToAction(nameof(Index));
            }

            // Check permissions and apply role-based restrictions
            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            if (!userRoles.Contains("Administrator") && !userRoles.Contains("FrontOffice"))
            {
                // Patients can only edit their own appointments
                if (userRoles.Contains("Patient"))
                {
                    if (originalAppointment.PatientId != user!.Id)
                    {
                        return Forbid();
                    }
                    // Patients cannot change the patient or status
                    appointment.PatientId = originalAppointment.PatientId;
                    appointment.Status = originalAppointment.Status;
                }
                // Doctors can only edit appointments assigned to them
                else if (userRoles.Contains("Doctor") || userRoles.Contains("DentalHygienist"))
                {
                    if (originalAppointment.DoctorId != user!.Id)
                    {
                        return Forbid();
                    }
                    // Doctors cannot change the patient
                    appointment.PatientId = originalAppointment.PatientId;
                }
            }

            // Check if the new time slot is available (only if time or service changed)
            if (appointment.AppointmentDateTime != originalAppointment.AppointmentDateTime ||
                appointment.ServiceId != originalAppointment.ServiceId ||
                appointment.DoctorId != originalAppointment.DoctorId)
            {
                // Validate date/time changes - ensure we compare times in the same timezone
                var appointmentLocal = appointment.AppointmentDateTime.Kind == DateTimeKind.Utc 
                    ? appointment.AppointmentDateTime.ToLocalTime() 
                    : appointment.AppointmentDateTime;
                var currentLocal = DateTime.Now;
                
                if (appointmentLocal <= currentLocal)
                {
                    ModelState.AddModelError("AppointmentDateTime", "Appointment date and time must be in the future.");
                }

                // Check if the appointment is on a weekday
                if (appointment.AppointmentDateTime.DayOfWeek == DayOfWeek.Saturday || 
                    appointment.AppointmentDateTime.DayOfWeek == DayOfWeek.Sunday)
                {
                    ModelState.AddModelError("AppointmentDateTime", "Appointments are only available on weekdays (Monday-Friday).");
                }
                
                if (!await IsTimeSlotAvailableForEdit(appointment.DoctorId, appointment.ServiceId, appointment.AppointmentDateTime, appointment.Id))
                {
                    ModelState.AddModelError("AppointmentDateTime", "This time slot conflicts with an existing appointment. Please select a different time.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    appointment.ModifiedDate = DateTime.UtcNow;
                    _context.Update(appointment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            ViewBag.Patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.CurrentUserRoles = userRoles;

            return View(appointment);
        }

        // POST: Appointments/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            // Prevent cancelling already cancelled appointments
            if (appointment.Status == AppointmentStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "This appointment is already cancelled.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user!);

            // Check permissions
            // Administrator and FrontOffice can cancel any appointment
            if (userRoles.Contains("Administrator") || userRoles.Contains("FrontOffice"))
            {
                // Full access - no restrictions
            }
            else if (userRoles.Contains("Patient") && appointment.PatientId != user!.Id)
                return Forbid();

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.ModifiedDate = DateTime.UtcNow;
            
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.Id == id);
        }

        // Check if a time slot is available for a specific doctor and service
        private async Task<bool> IsTimeSlotAvailable(string doctorId, int serviceId, DateTime appointmentDateTime)
        {
            // Get the service to determine duration
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return false;

            var appointmentEndTime = appointmentDateTime.AddMinutes(service.DurationMinutes);

            // Check for any overlapping appointments for this doctor
            var conflictingAppointments = await _context.Appointments
                .Include(a => a.Service)
                .Where(a => a.DoctorId == doctorId && 
                           a.AppointmentDateTime.Date == appointmentDateTime.Date &&
                           a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            foreach (var existingAppointment in conflictingAppointments)
            {
                var existingEndTime = existingAppointment.AppointmentDateTime.AddMinutes(existingAppointment.Service?.DurationMinutes ?? 60);
                
                // Check if there's any overlap
                if (appointmentDateTime < existingEndTime && appointmentEndTime > existingAppointment.AppointmentDateTime)
                {
                    return false; // Conflict found
                }
            }

            return true; // No conflicts
        }

        // Get available time slots for a specific doctor on a specific date
        [HttpGet]
        public async Task<IActionResult> GetAvailableTimeSlots(string doctorId, int serviceId, DateTime date)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) 
            {
                return Json(new { success = false, message = "Service not found" });
            }

            var availableSlots = await GetAvailableTimeSlotsForDate(doctorId, serviceId, date);
            
            return Json(new { 
                success = true, 
                slots = availableSlots.Select(slot => new {
                    time = slot.ToString("HH:mm"),
                    display = slot.ToString("h:mm tt")
                })
            });
        }

        // Helper method to get available time slots for a specific date
        private async Task<List<DateTime>> GetAvailableTimeSlotsForDate(string doctorId, int serviceId, DateTime date)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return new List<DateTime>();

            var availableSlots = new List<DateTime>();
            
            // Office hours: 9 AM to 5 PM
            var startTime = date.Date.AddHours(9);
            var endTime = date.Date.AddHours(17);

            // Generate potential time slots every 30 minutes
            for (var time = startTime; time.AddMinutes(service.DurationMinutes) <= endTime; time = time.AddMinutes(30))
            {
                // Skip weekends
                if (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                // Skip past dates
                if (time <= DateTime.Now)
                    continue;

                // Check if this time slot is available
                if (await IsTimeSlotAvailable(doctorId, serviceId, time))
                {
                    availableSlots.Add(time);
                }
            }

            return availableSlots;
        }

        // Check if a time slot is available for editing (excludes the appointment being edited)
        private async Task<bool> IsTimeSlotAvailableForEdit(string doctorId, int serviceId, DateTime appointmentDateTime, int excludeAppointmentId)
        {
            // Get the service to determine duration
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return false;

            var appointmentEndTime = appointmentDateTime.AddMinutes(service.DurationMinutes);

            // Check for any overlapping appointments for this doctor (excluding the current appointment)
            var conflictingAppointments = await _context.Appointments
                .Include(a => a.Service)
                .Where(a => a.DoctorId == doctorId && 
                           a.Id != excludeAppointmentId &&
                           a.AppointmentDateTime.Date == appointmentDateTime.Date &&
                           a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            foreach (var existingAppointment in conflictingAppointments)
            {
                var existingEndTime = existingAppointment.AppointmentDateTime.AddMinutes(existingAppointment.Service?.DurationMinutes ?? 60);
                
                // Check if there's any overlap
                if (appointmentDateTime < existingEndTime && appointmentEndTime > existingAppointment.AppointmentDateTime)
                {
                    return false; // Conflict found
                }
            }

            return true; // No conflicts
        }
    }
}