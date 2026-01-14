using DentalClinicWebApp.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DentalClinicWebApp.Tests
{
    /// <summary>
    /// Simple test class to verify credit card validation
    /// </summary>
    public static class CreditCardValidationTest
    {
        public static void RunTests()
        {
            Console.WriteLine("Running Credit Card Validation Tests...");
            
            var validator = new CreditCardValidationAttribute();
            
            // Test valid credit card numbers (common test numbers)
            var validNumbers = new[]
            {
                "4532015112830366", // Visa
                "5555555555554444", // MasterCard
                "378282246310005",  // American Express
                "6011111111111117", // Discover
                "4000 0000 0000 0002", // Visa with spaces
                "5555-5555-5555-4444"  // MasterCard with dashes
            };
            
            // Test invalid credit card numbers
            var invalidNumbers = new[]
            {
                "4532015112830367", // Invalid Luhn
                "1234567890123456", // Invalid Luhn
                "0000000000000000", // All zeros
                "1111111111111111", // All same digit
                "123",              // Too short
                "12345678901234567890", // Too long
                "abcd1234567890123", // Contains letters
                "",                 // Empty
                null                // Null
            };
            
            Console.WriteLine("\nTesting VALID credit card numbers:");
            foreach (var number in validNumbers)
            {
                var isValid = validator.IsValid(number);
                Console.WriteLine($"  {number ?? "null"}: {(isValid ? "✓ VALID" : "✗ INVALID")}");
                if (!isValid && number != null)
                {
                    Console.WriteLine($"    ERROR: Expected valid but got invalid!");
                }
            }
            
            Console.WriteLine("\nTesting INVALID credit card numbers:");
            foreach (var number in invalidNumbers)
            {
                var isValid = validator.IsValid(number);
                Console.WriteLine($"  {number ?? "null"}: {(isValid ? "✗ VALID (ERROR!)" : "✓ INVALID")}");
                if (isValid && number != null && number != "")
                {
                    Console.WriteLine($"    ERROR: Expected invalid but got valid!");
                }
            }
            
            Console.WriteLine("\nCredit Card Validation Tests Completed!");
        }
    }
}