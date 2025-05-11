using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Services
{
    public class ConnectionStringManager : IConnectionStringManager
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConnectionStringManager> _logger;
        private readonly IEncryptionService _encryptionService;

        public ConnectionStringManager(
            IConfiguration configuration,
            ILogger<ConnectionStringManager> logger,
            IEncryptionService encryptionService)
        {
            _configuration = configuration;
            _logger = logger;
            _encryptionService = encryptionService;
        }

        public async Task<SqlConnection> CreateSourceConnectionAsync(int importDataId, CancellationToken cancellationToken = default)
        {
            var connectionInfo = await GetConnectionInfoAsync(importDataId, isSource: true, cancellationToken);
            return new SqlConnection(BuildConnectionString(connectionInfo));
        }

        public async Task<SqlConnection> CreateDestinationConnectionAsync(int importDataId, CancellationToken cancellationToken = default)
        {
            var connectionInfo = await GetConnectionInfoAsync(importDataId, isSource: false, cancellationToken);
            return new SqlConnection(BuildConnectionString(connectionInfo));
        }

        private async Task<ConnectionInfo> GetConnectionInfoAsync(int importDataId, bool isSource, CancellationToken cancellationToken)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // First check if we have a directly matching schema (using our renamed columns)
            string queryToCheckSchema = @"
                SELECT 
                    COUNT(*) as ColumnCount
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'ImportData' 
                AND COLUMN_NAME IN ('FromDataBase', 'ToDataBase')";

            using (var checkCmd = new SqlCommand(queryToCheckSchema, connection))
            {
                int columnsFound = (int)await checkCmd.ExecuteScalarAsync(cancellationToken);
                bool hasDirectDbColumns = columnsFound == 2;

                string sqlQuery;
                if (hasDirectDbColumns)
                {
                    // Schema has the explicit FromDataBase and ToDataBase columns as expected
                    _logger.LogInformation("Using explicit FromDataBase/ToDataBase columns");
                    sqlQuery = @"
                        SELECT 
                            i.Id AS ImportId,
                            i.FromDataBase,
                            i.ToDataBase,
                            CASE WHEN @IsSource = 1 THEN ds.ServerName ELSE dd.ServerName END AS ServerName,
                            CASE WHEN @IsSource = 1 THEN ds.UserName ELSE dd.UserName END AS UserName,
                            CASE WHEN @IsSource = 1 THEN ds.Password ELSE dd.Password END AS Password,
                            CASE WHEN @IsSource = 1 THEN ds.Authentication ELSE dd.Authentication END AS Authentication,
                            CASE WHEN @IsSource = 1 THEN i.FromDataBase ELSE i.ToDataBase END AS DatabaseName
                        FROM ImportData i
                        JOIN DataSource ds ON ds.DataSourceId = i.FromConnectionId
                        JOIN DataSource dd ON dd.DataSourceId = i.ToConnectionId
                        WHERE i.Id = @ImportId";
                }
                else
                {
                    // Fall back to using legacy schema (defaultDatabaseName from DataSource)
                    _logger.LogInformation("Falling back to use DataSource.DefaultDatabaseName");
                    sqlQuery = @"
                        SELECT 
                            i.Id AS ImportId,
                            ds.DefaultDatabaseName AS FromDataBase,
                            dd.DefaultDatabaseName AS ToDataBase,
                            CASE WHEN @IsSource = 1 THEN ds.ServerName ELSE dd.ServerName END AS ServerName,
                            CASE WHEN @IsSource = 1 THEN ds.UserName ELSE dd.UserName END AS UserName,
                            CASE WHEN @IsSource = 1 THEN ds.Password ELSE dd.Password END AS Password,
                            CASE WHEN @IsSource = 1 THEN ds.Authentication ELSE dd.Authentication END AS Authentication,
                            CASE WHEN @IsSource = 1 THEN ds.DefaultDatabaseName ELSE dd.DefaultDatabaseName END AS DatabaseName
                        FROM ImportData i
                        JOIN DataSource ds ON ds.DataSourceId = i.FromConnectionId
                        JOIN DataSource dd ON dd.DataSourceId = i.ToConnectionId
                        WHERE i.Id = @ImportId";
                }

                using var command = new SqlCommand(sqlQuery, connection);
                command.Parameters.AddWithValue("@ImportId", importDataId);
                command.Parameters.AddWithValue("@IsSource", isSource);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var info = new ConnectionInfo
                    {
                        ServerName = reader.GetString(reader.GetOrdinal("ServerName")),
                        DatabaseName = reader.GetString(reader.GetOrdinal("DatabaseName")),
                        UserName = reader.GetString(reader.GetOrdinal("UserName")),
                        Password = reader.GetString(reader.GetOrdinal("Password")),
                        Authentication = reader.GetString(reader.GetOrdinal("Authentication"))
                    };
                    
                    _logger.LogInformation("Connection info loaded for Import ID {ImportId}, Target DB: {DatabaseName}", 
                        importDataId, info.DatabaseName);
                    
                    return info;
                }
                
                throw new Exception($"Import data not found for ID: {importDataId}");
            }
        }

        private string BuildConnectionString(ConnectionInfo connectionInfo)
        {
            // Validate connection info before using it
            ValidateConnectionInfo(connectionInfo);

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = connectionInfo.ServerName,
                InitialCatalog = connectionInfo.DatabaseName,
                TrustServerCertificate = true,
                ConnectTimeout = 30
            };
            
            // Handle authentication type
            if (connectionInfo.Authentication.Equals("Windows Authentication", StringComparison.OrdinalIgnoreCase))
            {
                builder.IntegratedSecurity = true;
            }
            else // SQL Server Authentication
            {
                // Decrypt password
                string decryptedPassword = _encryptionService.Decrypt(connectionInfo.Password);
                
                // Additional check to ensure we have a valid password after decryption
                if (string.IsNullOrEmpty(decryptedPassword))
                {
                    _logger.LogWarning("Decrypted password is empty for server {ServerName}, database {DatabaseName}", 
                        connectionInfo.ServerName, connectionInfo.DatabaseName);
                }
                
                builder.UserID = connectionInfo.UserName;
                builder.Password = decryptedPassword;
                builder.IntegratedSecurity = false;
            }
            
            // Log connection string with password masked for debugging
            string logConnectionString = builder.ConnectionString;
            
            // Only attempt to mask password if it's not empty
            if (!string.IsNullOrEmpty(builder.Password))
            {
                logConnectionString = logConnectionString.Replace(builder.Password, "********");
            }
            
            _logger.LogDebug("Built connection string: {ConnectionString}", logConnectionString);
            
            return builder.ConnectionString;
        }

        /// <summary>
        /// Validates the connection info to ensure all required fields are present
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a required field is missing</exception>
        private void ValidateConnectionInfo(ConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
            {
                throw new ArgumentNullException(nameof(connectionInfo), "Connection information cannot be null");
            }
            
            if (string.IsNullOrEmpty(connectionInfo.ServerName))
            {
                throw new ArgumentException("Server name cannot be empty", nameof(connectionInfo.ServerName));
            }

            if (string.IsNullOrEmpty(connectionInfo.DatabaseName))
            {
                throw new ArgumentException("Database name cannot be empty", nameof(connectionInfo.DatabaseName));
            }
            
            if (string.IsNullOrEmpty(connectionInfo.Authentication))
            {
                throw new ArgumentException("Authentication type cannot be empty", nameof(connectionInfo.Authentication));
            }
            
            // For SQL Server authentication, we need a username
            if (!connectionInfo.Authentication.Equals("Windows Authentication", StringComparison.OrdinalIgnoreCase) 
                && string.IsNullOrEmpty(connectionInfo.UserName))
            {
                throw new ArgumentException("Username cannot be empty for SQL Server Authentication", nameof(connectionInfo.UserName));
            }
        }
        
        /// <summary>
        /// Private class to hold connection information
        /// </summary>
        private class ConnectionInfo
        {
            public string ServerName { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Authentication { get; set; } = string.Empty;
        }
    }
} 