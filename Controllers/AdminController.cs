using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using DentalClinicWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

namespace DentalClinicWebApp.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
        }

        // Simple action to assign Patient role to current user (available to all authenticated users)
        public async Task<IActionResult> AssignPatientRole()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                if (!userRoles.Contains("Patient"))
                {
                    await _userManager.AddToRoleAsync(user, "Patient");
                    TempData["Message"] = "Patient role assigned successfully! You can now create appointments.";
                }
                else
                {
                    TempData["Message"] = "You already have the Patient role assigned.";
                }
            }
            
            return RedirectToAction("Index", "Home");
        }

        // Action to show current user info and roles
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UserInfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.User = user;
                var userRoles = await _userManager.GetRolesAsync(user);
                ViewBag.Roles = userRoles;
                ViewBag.AllRoles = _roleManager.Roles.ToList();
                
                // Check admin status from database, not from cached session
                ViewBag.IsAdmin = userRoles.Contains("Administrator");
                
                // If user is admin, get all users for role management
                if (userRoles.Contains("Administrator"))
                {
                    var allUsers = await _userManager.Users.ToListAsync();
                    var usersWithRoles = new List<(ApplicationUser User, IList<string> Roles)>();
                    
                    foreach (var u in allUsers)
                    {
                        var roles = await _userManager.GetRolesAsync(u);
                        usersWithRoles.Add((u, roles));
                    }
                    
                    ViewBag.AllUsers = usersWithRoles;
                }
            }
            
            return View();
        }

        // Action to assign a specific role to current user (restricted: only Patient role for non-administrators)
        [HttpPost]
        public async Task<IActionResult> AssignRole(string roleName)
        {
            if (string.IsNullOrEmpty(roleName))
            {
                TempData["Error"] = "Please select a role.";
                return RedirectToAction("UserInfo");
            }

            // Non-administrators can ONLY assign themselves the Patient role
            if (!User.IsInRole("Administrator"))
            {
                if (roleName != "Patient")
                {
                    TempData["Error"] = "Access denied. Only administrators can assign non-Patient roles.";
                    return RedirectToAction("UserInfo");
                }
            }

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                if (!userRoles.Contains(roleName))
                {
                    await _userManager.AddToRoleAsync(user, roleName);
                    TempData["Message"] = $"{roleName} role assigned successfully!";
                }
                else
                {
                    TempData["Message"] = $"You already have the {roleName} role assigned.";
                }
            }
            
            return RedirectToAction("UserInfo");
        }

        // Action to assign role to any user (administrators only)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AssignRoleToUser(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
            {
                TempData["Error"] = "Invalid user or role selection.";
                return RedirectToAction("UserInfo");
            }

            // Additional security: verify current user is actually an administrator
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser!);
            if (!currentUserRoles.Contains("Administrator"))
            {
                TempData["Error"] = "Access denied. Administrator role required.";
                return RedirectToAction("UserInfo");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("UserInfo");
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(roleName))
            {
                await _userManager.AddToRoleAsync(user, roleName);
                TempData["Message"] = $"{roleName} role assigned to {user.FirstName} {user.LastName} successfully!";
            }
            else
            {
                TempData["Message"] = $"{user.FirstName} {user.LastName} already has the {roleName} role.";
            }
            
            return RedirectToAction("UserInfo");
        }

        // Action to remove role from any user (administrators only)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> RemoveRoleFromUser(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
            {
                TempData["Error"] = "Invalid user or role selection.";
                return RedirectToAction("UserInfo");
            }

            // Additional security: verify current user is actually an administrator
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser!);
            if (!currentUserRoles.Contains("Administrator"))
            {
                TempData["Error"] = "Access denied. Administrator role required.";
                return RedirectToAction("UserInfo");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("UserInfo");
            }

            // Prevent removing Administrator role from the default admin user
            if (roleName == "Administrator" && user.Email == "admin@contosodentalclinic.com")
            {
                TempData["Error"] = "Cannot remove Administrator role from the default admin account.";
                return RedirectToAction("UserInfo");
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains(roleName))
            {
                await _userManager.RemoveFromRoleAsync(user, roleName);
                TempData["Message"] = $"{roleName} role removed from {user.FirstName} {user.LastName} successfully!";
            }
            else
            {
                TempData["Message"] = $"{user.FirstName} {user.LastName} does not have the {roleName} role.";
            }
            
            return RedirectToAction("UserInfo");
        }

        // Action to clean up admin roles from regular users (administrators only)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CleanupAdminRoles()
        {
            // Additional security: verify current user is actually an administrator
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser!);
            if (!currentUserRoles.Contains("Administrator"))
            {
                TempData["Error"] = "Access denied. Administrator role required.";
                return RedirectToAction("UserInfo");
            }

            var adminUsers = await _userManager.GetUsersInRoleAsync("Administrator");
            var defaultAdminEmail = "admin@contosodentalclinic.com";
            var removedCount = 0;

            foreach (var user in adminUsers)
            {
                // Skip the default admin account
                if (user.Email != defaultAdminEmail)
                {
                    await _userManager.RemoveFromRoleAsync(user, "Administrator");
                    removedCount++;
                }
            }

            TempData["Message"] = $"Administrator role removed from {removedCount} regular user(s). Only the default admin account retains administrator privileges.";
            return RedirectToAction("UserInfo");
        }

        // Action to refresh the current user's authentication session
        public async Task<IActionResult> RefreshSession()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // Sign out and sign back in to refresh the authentication session
                await _signInManager.SignOutAsync();
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["Message"] = "Your session has been refreshed with current role information.";
            }
            
            return RedirectToAction("UserInfo");
        }

        // Action to show all users for management (administrators only)
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ManageUsers()
        {
            // Additional security: verify current user is actually an administrator
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser!);
            if (!currentUserRoles.Contains("Administrator"))
            {
                TempData["Error"] = "Access denied. Administrator role required.";
                return RedirectToAction("UserInfo");
            }

            var allUsers = await _userManager.Users.ToListAsync();
            var usersWithRoles = new List<(ApplicationUser User, IList<string> Roles)>();
            
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                usersWithRoles.Add((user, roles));
            }
            
            ViewBag.AllUsers = usersWithRoles;
            return View();
        }

        // Action to edit user information (administrators only)
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "User ID is required.";
                return RedirectToAction("ManageUsers");
            }

            // Additional security: verify current user is actually an administrator
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser!);
            if (!currentUserRoles.Contains("Administrator"))
            {
                TempData["Error"] = "Access denied. Administrator role required.";
                return RedirectToAction("UserInfo");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUsers");
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;
            ViewBag.AllRoles = _roleManager.Roles.ToList();

            return View(user);
        }

        // Action to update user information (administrators only)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> EditUser(string id, string firstName, string lastName, DateTime dateOfBirth, string address, string phoneNumber, string gender)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "User ID is required.";
                return RedirectToAction("ManageUsers");
            }

            // Additional security: verify current user is actually an administrator
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser!);
            if (!currentUserRoles.Contains("Administrator"))
            {
                TempData["Error"] = "Access denied. Administrator role required.";
                return RedirectToAction("UserInfo");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUsers");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                TempData["Error"] = "First name and last name are required.";
                return RedirectToAction("EditUser", new { id });
            }

            // Update user information
            user.FirstName = firstName.Trim();
            user.LastName = lastName.Trim();
            user.DateOfBirth = dateOfBirth;
            user.Address = address?.Trim() ?? string.Empty;
            user.PhoneNumber = phoneNumber?.Trim();
            user.Gender = gender?.Trim() ?? string.Empty;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Message"] = $"User information updated successfully for {user.FirstName} {user.LastName}.";
            }
            else
            {
                TempData["Error"] = "Failed to update user information: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageUsers");
        }
    }
}