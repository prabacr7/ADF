using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DataTransfer.Infrastructure.Services
{
    /// <summary>
    /// Test utility to decrypt the specific value directly
    /// </summary>
    public static class EncryptionTest
    {
        public static string DecryptSpecificValue()
        {
            try
            {
                string encryptedValue = "q77saCXidcezg4/hZtsSow==";
                string hardcodedKey = "MAKV2SPBNI99212";
                
                Console.WriteLine($"Testing decryption of: {encryptedValue}");
                Console.WriteLine($"Using key: {hardcodedKey}");
                
                byte[] cipherBytes = Convert.FromBase64String(encryptedValue);
                
                using (Aes aes = Aes.Create())
                {
                    var pdb = new Rfc2898DeriveBytes(hardcodedKey, new byte[] {
                        0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64,
                        0x76, 0x65, 0x64, 0x65, 0x76 });
                    aes.Key = pdb.GetBytes(32);
                    aes.IV = pdb.GetBytes(16);
                    
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            try
                            {
                                cs.Write(cipherBytes, 0, cipherBytes.Length);
                                cs.Close(); // Use Close() instead of FlushFinalBlock()
                                
                                // Try multiple encodings
                                string unicodeResult = Encoding.Unicode.GetString(ms.ToArray());
                                string utf8Result = Encoding.UTF8.GetString(ms.ToArray());
                                string asciiResult = Encoding.ASCII.GetString(ms.ToArray());
                                
                                Console.WriteLine($"Unicode result: '{unicodeResult}'");
                                Console.WriteLine($"UTF8 result: '{utf8Result}'");
                                Console.WriteLine($"ASCII result: '{asciiResult}'");
                                
                                if (unicodeResult == "Inatech@123")
                                {
                                    Console.WriteLine("✅ SUCCESS: Decrypted to expected value 'Inatech@123'");
                                }
                                else
                                {
                                    Console.WriteLine("❌ FAILED: Did not get expected 'Inatech@123'");
                                }
                                
                                return unicodeResult;
                            }
                            catch (CryptographicException ex)
                            {
                                Console.WriteLine($"Error during cryptographic operation: {ex.Message}");
                                return "ERROR: " + ex.Message;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error: {ex.Message}");
                return "ERROR: " + ex.Message;
            }
        }
    }
} 