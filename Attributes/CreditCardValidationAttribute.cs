using System.ComponentModel.DataAnnotations;

namespace DentalClinicWebApp.Attributes
{
    /// <summary>
    /// Validates credit card numbers using the Luhn algorithm
    /// </summary>
    public class CreditCardValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return true; // Let Required attribute handle empty values

            var cardNumber = value.ToString()!.Replace(" ", "").Replace("-", "");
            
            // Check if the string contains only digits
            if (!cardNumber.All(char.IsDigit))
                return false;

            // Check length (most cards are 13-19 digits)
            if (cardNumber.Length < 13 || cardNumber.Length > 19)
                return false;

            // Reject common invalid patterns
            if (cardNumber.All(c => c == '0') || // All zeros
                cardNumber.All(c => c == cardNumber[0])) // All same digit
                return false;

            // Validate using Luhn algorithm
            return IsValidLuhn(cardNumber);
        }

        /// <summary>
        /// Validates a credit card number using the Luhn algorithm
        /// </summary>
        /// <param name="cardNumber">The credit card number (digits only)</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool IsValidLuhn(string cardNumber)
        {
            int sum = 0;
            bool alternate = false;

            // Process digits from right to left
            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int digit = int.Parse(cardNumber[i].ToString());

                if (alternate)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit = (digit % 10) + 1;
                }

                sum += digit;
                alternate = !alternate;
            }

            return (sum % 10) == 0;
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"The {name} field contains an invalid credit card number.";
        }
    }
}