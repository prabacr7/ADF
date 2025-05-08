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
            try
            {
                byte[] key = Encoding.UTF8.GetBytes(_encryptionKey);
                byte[] iv = new byte[16]; // Initialization vector
                
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    
                    var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                    
                    using (var memoryStream = new System.IO.MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (var streamWriter = new System.IO.StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(password);
                            }
                            return Convert.ToBase64String(memoryStream.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting password: {Message}", ex.Message);
                throw;
            }
        }

        private string DecryptPassword(string encryptedPassword)
        {
            try
            {
                byte[] key = Encoding.UTF8.GetBytes(_encryptionKey);
                byte[] iv = new byte[16]; // Initialization vector
                byte[] buffer = Convert.FromBase64String(encryptedPassword);
                
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    
                    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    
                    using (var memoryStream = new System.IO.MemoryStream(buffer))
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (var streamReader = new System.IO.StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting password: {Message}", ex.Message);
                return string.Empty;
            }
        }
    }
} 