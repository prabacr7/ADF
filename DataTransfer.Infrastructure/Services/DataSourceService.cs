using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Services
{
    public class DataSourceService : IDataSourceService
    {
        private readonly IDataSourceRepository _dataSourceRepository;
        private readonly ILogger<DataSourceService> _logger;
        private readonly string _encryptionKey = "A68DB9853C61D4F684CA92001C4B7841"; // In production, use a secure key vault

        public DataSourceService(IDataSourceRepository dataSourceRepository, ILogger<DataSourceService> logger)
        {
            _dataSourceRepository = dataSourceRepository;
            _logger = logger;
        }

        public async Task<bool> TestConnectionAsync(string serverName, string userName, string password, 
            string authenticationType, string defaultDatabaseName)
        {
            try
            {
                var connectionString = BuildConnectionString(serverName, userName, password, authenticationType, defaultDatabaseName);
                
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing SQL Server connection: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<DataSource> SaveDataSourceAsync(DataSource dataSource)
        {
            try
            {
                // Encrypt the password before saving
                if (!string.IsNullOrEmpty(dataSource.Password))
                {
                    dataSource.Password = EncryptPassword(dataSource.Password);
                }
                
                // The IsActive property is ignored by EF Core, so no need to set it
                
                return await _dataSourceRepository.AddAsync(dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving data source: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<DataSource> GetDataSourceByIdAsync(int id)
        {
            var dataSource = await _dataSourceRepository.GetByIdAsync(id);
            
            // Decrypt password if it exists
            if (dataSource != null && !string.IsNullOrEmpty(dataSource.Password))
            {
                dataSource.Password = DecryptPassword(dataSource.Password);
            }
            
            return dataSource;
        }

        public async Task<IEnumerable<DataSource>> GetAllDataSourcesAsync(int? userId = null)
        {
            return await _dataSourceRepository.GetAllAsync(userId);
        }

        public async Task<bool> UpdateDataSourceAsync(DataSource dataSource)
        {
            // Encrypt the password before updating
            if (!string.IsNullOrEmpty(dataSource.Password))
            {
                // Check if the password has changed (assuming encrypted passwords start with a specific pattern)
                var existingDataSource = await _dataSourceRepository.GetByIdAsync(dataSource.DataSourceId);
                if (existingDataSource != null && dataSource.Password != existingDataSource.Password)
                {
                    dataSource.Password = EncryptPassword(dataSource.Password);
                }
            }
            
            return await _dataSourceRepository.UpdateAsync(dataSource);
        }

        public async Task<bool> DeleteDataSourceAsync(int id)
        {
            // Since we can't mark as inactive (no IsActive column), we'll actually delete the record
            return await _dataSourceRepository.DeleteAsync(id);
        }

        private string BuildConnectionString(string serverName, string userName, string password, 
            string authenticationType, string defaultDatabaseName)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = defaultDatabaseName ?? "master"
            };

            if (authenticationType?.ToLower() == "sql server authentication")
            {
                builder.UserID = userName;
                builder.Password = password;
            }
            else // Windows Authentication
            {
                builder.IntegratedSecurity = true;
            }

            builder.TrustServerCertificate = true;
            builder.ConnectTimeout = 30;
            
            return builder.ConnectionString;
        }

        private string EncryptPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return string.Empty;

            try
            {
                string EncryptionKey = "MAKV2SPBNI99212";
                byte[] clearBytes = Encoding.Unicode.GetBytes(password);

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
                _logger.LogError(ex, "Error encrypting password: {Message}", ex.Message);
                throw;
            }
        }

        private string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrWhiteSpace(encryptedPassword))
                return string.Empty;

            try
            {
                string EncryptionKey = "MAKV2SPBNI99212";
                byte[] cipherBytes = Convert.FromBase64String(encryptedPassword);

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
                _logger.LogError(ex, "Error decrypting password: {Message}", ex.Message);
                return string.Empty;
            }
        }
    }
} 