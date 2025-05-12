using DataTransfer.Infrastructure.Services;
using System;

namespace DataTransfer.Infrastructure
{
    class TestRunner
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===== Testing Encryption/Decryption =====");
            Console.WriteLine();
            
            try
            {
                string result = EncryptionTest.DecryptSpecificValue();
                Console.WriteLine($"Final result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine();
            Console.WriteLine("===== Test Complete =====");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
} 