using DataTransfer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DataTransfer.Infrastructure.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private readonly string _encryptionKey;
        
        public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
        {
            _logger = logger;
            
            // Get the encryption key from configuration or use a default one (not recommended for production)
            _encryptionKey = configuration["EncryptionSettings:Key"] ?? "DataTransfer12345678901234567890Key!";
            
            if (_encryptionKey == "DataTransfer12345678901234567890Key!")
            {
                _logger.LogWarning("Using default encryption key. This is not recommended for production environments.");
            }
        }
        
        public string Decrypt(string encryptedValue)
        {
            if (string.IsNullOrEmpty(encryptedValue))
            {
                return string.Empty;
            }
            
            try
            {
                // Convert the encrypted value from base64
                byte[] cipherBytes = Convert.FromBase64String(encryptedValue);
                
                using (Aes aes = Aes.Create())
                {
                    // Key derivation
                    using (var deriveBytes = new Rfc2898DeriveBytes(_encryptionKey, 
                        new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }, 
                        1000, HashAlgorithmName.SHA256))
                    {
                        aes.Key = deriveBytes.GetBytes(32);
                        aes.IV = deriveBytes.GetBytes(16);
                    }
                    
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);
                            cryptoStream.FlushFinalBlock();
                        }
                        
                        byte[] decryptedBytes = memoryStream.ToArray();
                        return Encoding.UTF8.GetString(decryptedBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting value");
                return string.Empty;
            }
        }
        
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }
            
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                
                using (Aes aes = Aes.Create())
                {
                    // Key derivation
                    using (var deriveBytes = new Rfc2898DeriveBytes(_encryptionKey, 
                        new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }, 
                        1000, HashAlgorithmName.SHA256))
                    {
                        aes.Key = deriveBytes.GetBytes(32);
                        aes.IV = deriveBytes.GetBytes(16);
                    }
                    
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                            cryptoStream.FlushFinalBlock();
                        }
                        
                        byte[] encryptedBytes = memoryStream.ToArray();
                        return Convert.ToBase64String(encryptedBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting value");
                return string.Empty;
            }
        }
    }
} 