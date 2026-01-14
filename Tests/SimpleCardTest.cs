using DentalClinicWebApp.Attributes;

namespace DentalClinicWebApp.Tests
{
    public static class SimpleCardTest
    {
        public static void RunTest()
        {
            var validator = new CreditCardValidationAttribute();
            
            var testCard = "0000000000000000";
            Console.WriteLine($"Testing card: {testCard}");
            
            var cleaned = testCard.Replace(" ", "").Replace("-", "");
            Console.WriteLine($"Cleaned: {cleaned}");
            Console.WriteLine($"Length: {cleaned.Length}");
            Console.WriteLine($"All digits: {cleaned.All(char.IsDigit)}");
            Console.WriteLine($"All zeros: {cleaned.All(c => c == '0')}");
            Console.WriteLine($"All same digit: {cleaned.All(c => c == cleaned[0])}");
            
            var result = validator.IsValid(testCard);
            Console.WriteLine($"Validation result: {result}");
        }
    }
}