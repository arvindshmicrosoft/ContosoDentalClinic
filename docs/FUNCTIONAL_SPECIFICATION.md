# Functional Specification

This document defines the detailed requirements and user workflows for the Contoso University Dental Clinic application. For setup instructions and technical overview, see the main [README](../README.md). This project is licensed under the [MIT License](../LICENSE).

---

## "I-Can" Statements by Role

### Patient
- I can register with my complete personal information (name, date of birth, email)
- I can log in with my email and password
- I can view my personal information and current roles
- I can assign myself the Patient role if not already assigned
- I can browse available dental services
- I can schedule appointments only during available time slots that accommodate my selected service duration
- I can reschedule my existing appointments to any available time slot
- I can change the doctor or service type when editing my appointments (subject to availability)
- I can view appointment details including service duration and pricing
- I can add and edit notes for my appointments
- I can view and pay my invoices using multiple payment methods
- I can access my treatment history and medical records

### Front Office
- I can view and manage all patient appointments with full scheduling flexibility
- I can create appointments for any patient with any available doctor and service
- I can modify appointment times, doctors, and services for any patient (subject to availability)
- I can view real-time doctor availability and service duration requirements
- I can resolve scheduling conflicts and optimize appointment booking
- I can process billing and handle payment transactions
- I can view patient records and contact information
- I can generate and manage invoices

### Doctor
- I can view my assigned patient appointments with detailed service and duration information
- I can edit appointment details for my assigned patients (notes, service modifications)
- I can see my schedule organized by appointment duration and service types
- I can update patient records and treatment notes
- I can manage patient treatment plans and medical history
- I can view patient information relevant to care

### Dental Hygienist
- I can view my assigned patient appointments with service duration and scheduling details
- I can edit my assigned appointments within my scope of practice
- I can update patient hygiene records and treatment notes
- I can access patient oral health history
- I can document hygiene treatments and recommendations

### Administrator
- I can view and edit all user information (names, contact details, dates of birth)
- I can assign and remove roles for any user (except protected default admin)
- I can manage user accounts and update personal information
- I can access comprehensive user management tools
- I can clean up unauthorized admin role assignments
- I can view system-wide user statistics and role distributions
- I can manage dental services and staff assignments
- I can access all patient records and system data
- I can refresh user sessions to resolve role inconsistencies

---

## Appointment Scheduling System

### Service Duration Management

| Service Type | Duration | Notes |
|--------------|----------|-------|
| Standard appointments | 60 minutes | Most services |
| Deep Cleaning | 120 minutes | Extended procedure |
| Metal Frame (Removable Partial) | 120 minutes | Extended procedure |

Durations are stored in the database and can be modified by administrators.

### Doctor Availability Rules

- **Conflict Prevention**: System prevents double-booking of doctors at overlapping times
- **Overlap Detection**: Checks for time conflicts considering full service duration, not just start times
- **Real-time Validation**: Both appointment creation and editing validate against existing bookings
- **Self-Exclusion**: When editing, current appointment is excluded from conflict checking

### Time Slot Generation

- **Office Hours**: Monday–Friday, 9:00 AM to 5:00 PM
- **Slot Intervals**: 30-minute intervals
- **Service-Aware**: Only shows slots where the full service duration fits within office hours
- **Weekend Exclusion**: Saturdays and Sundays are not available
- **Dynamic Loading**: Time slots update automatically when doctor, service, or date changes

### User Interface Behavior

- Requires doctor, service, and date selection before showing time slots
- Time slot dropdown refreshes when any prerequisite field changes
- Shows selected service details including duration and price
- Clear error messages when selected times conflict with existing appointments

---

## Payment Processing

### Supported Methods

| Method | Required Fields |
|--------|-----------------|
| **Credit Card** | Card number, expiration date, CVV, cardholder name |
| **Check** | Check number, bank routing number, account number |
| **Bank Account** | Account type (Checking/Savings), routing number, account number |

### Validation Rules

- Credit card numbers validated with Luhn algorithm
- Card numbers masked in display (show last 4 digits only)
- CVV never stored, only validated
- Routing numbers validated for format
- All payment data validated server-side

---

## User Interface Specifications

### Registration Page (`/Identity/Account/Register`)

| Field | Required | Validation |
|-------|----------|------------|
| First Name | Yes | Non-empty |
| Last Name | Yes | Non-empty |
| Date of Birth | Yes | Valid date, must be in past |
| Email | Yes | Valid email format |
| Password | Yes | Meets complexity requirements |
| Confirm Password | Yes | Must match password |

New users are automatically assigned the "Patient" role.

### Login Page (`/Identity/Account/Login`)

- Clean form with email and password fields
- "Remember Me" checkbox
- Links to registration and password reset
- No external authentication providers shown

### User Info Page (`/Admin/UserInfo`)

| User Type | Visible Features |
|-----------|------------------|
| Regular Users | View own info, self-assign Patient role |
| Administrators | Full user table, role management, edit any user |

### Role Management Security

- Only administrators can assign non-Patient roles
- Default admin account (`admin@contosodentalclinic.com`) cannot lose Administrator role
- Database validation prevents unauthorized role escalation
- Session refresh mechanism ensures role changes take effect

---

## Privacy & Compliance

### HIPAA Compliance Features

- Comprehensive privacy policy at `/Home/Privacy`
- Role-based access controls for medical data
- Audit trails for data access and modifications
- Secure handling of personal and medical information
- Clear explanation of data usage and patient rights

---

## Acceptance Criteria

- [x] Registration requires first name, last name, date of birth
- [x] Only administrators see role/user management in UserInfo
- [x] No external login/register options shown
- [x] Administrators can update user names and details
- [x] All "I-can" statements are supported for each role
- [x] Enhanced payment workflows with validation
- [x] HIPAA-compliant privacy policy
- [x] Role management security vulnerabilities resolved
- [x] Appointment scheduling respects service durations
- [x] Doctor availability prevents double-booking
