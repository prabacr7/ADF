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
            if (string.IsNullOrWhiteSpace(encryptedValue))
                return string.Empty;

            try
            {
                string EncryptionKey = "MAKV2SPBNI99212";
                byte[] cipherBytes = Convert.FromBase64String(encryptedValue);

                using (Aes encryptor = Aes.Create())
                {
                    var pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] {
                        0x49, 0x76, 0x61, 0x6e,
                        0x20, 0x4d, 0x65, 0x64,
                        0x76, 0x65, 0x64, 0x65,
                        0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);

                    using (var ms = new MemoryStream())
                    using (var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.FlushFinalBlock();
                        return Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Input is not valid Base64");
                return string.Empty;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Decryption failed: padding or key mismatch");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during decryption");
                return string.Empty;
            }
        }
        
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            try
            {
                string EncryptionKey = "MAKV2SPBNI99212";
                byte[] clearBytes = Encoding.Unicode.GetBytes(plainText);

                using (Aes encryptor = Aes.Create())
                {
                    var pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] {
                        0x49, 0x76, 0x61, 0x6e,
                        0x20, 0x4d, 0x65, 0x64,
                        0x76, 0x65, 0x64, 0x65,
                        0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);

                    using (var ms = new MemoryStream())
                    using (var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.FlushFinalBlock();
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Encryption failed: cryptographic error");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting value");
                return string.Empty;
            }
        }
    }
} 
