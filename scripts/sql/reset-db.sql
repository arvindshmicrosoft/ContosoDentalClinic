-- Database Reset Script - Clear Appointments and Invoices Data
-- This script will remove all appointments, invoices, invoice items, and payments
-- while preserving users, roles, and services

-- Clear appointments and related data
DELETE FROM [Appointments];

-- Clear invoices and related data (order matters due to foreign keys)
DELETE FROM [Payments];
DELETE FROM [InvoiceItems];
DELETE FROM [Invoices];

-- Reset identity columns to start from 1 again
DBCC CHECKIDENT ('Appointments', RESEED, 0);
DBCC CHECKIDENT ('Invoices', RESEED, 0);
DBCC CHECKIDENT ('InvoiceItems', RESEED, 0);
DBCC CHECKIDENT ('Payments', RESEED, 0);

PRINT 'Database reset completed successfully!';
PRINT 'All appointments and invoices have been cleared.';
PRINT 'Users, roles, and services have been preserved.';