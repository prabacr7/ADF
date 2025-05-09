using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DataTransfer.API.Services
{
    /// <summary>
    /// Factory for creating SQL connections with proper error handling and logging
    /// </summary>
    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqlConnectionFactory> _logger;

        public SqlConnectionFactory(IConfiguration configuration, ILogger<SqlConnectionFactory> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new SqlConnection using the preferred connection string
        /// </summary>
        /// <param name="preferredConnectionString">The preferred connection string name (optional)</param>
        /// <returns>A new SqlConnection instance</returns>
        public SqlConnection CreateConnection(string preferredConnectionString = null)
        {
            string connectionString = null;
            string connectionStringName = null;

            try
            {
                // First try the preferred connection string if provided
                if (!string.IsNullOrEmpty(preferredConnectionString))
                {
                    connectionString = _configuration.GetConnectionString(preferredConnectionString);
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        connectionStringName = preferredConnectionString;
                    }
                }

                // If preferred connection string not found, try alternatives in order
                if (string.IsNullOrEmpty(connectionString))
                {
                    // Try different connection string options in order of preference
                    string[] connectionStringNames = { "SQLHelper", "SqlServerConnection", "DefaultConnection" };
                    
                    foreach (var name in connectionStringNames)
                    {
                        connectionString = _configuration.GetConnectionString(name);
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            connectionStringName = name;
                            break;
                        }
                    }
                }

                // If still no connection string found, use fallback
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Database=ISQLHelper;Trusted_Connection=True;TrustServerCertificate=True;";
                    _logger.LogWarning("No connection string found in configuration. Using default local SQL Server connection string.");
                }
                else
                {
                    _logger.LogInformation("Using connection string: {ConnectionStringName}", connectionStringName);
                }

                return new SqlConnection(connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SQL connection: {ErrorMessage}", ex.Message);
                throw new ApplicationException("Failed to create database connection", ex);
            }
        }

        /// <summary>
        /// Creates and opens a new SqlConnection asynchronously
        /// </summary>
        /// <param name="preferredConnectionString">The preferred connection string name (optional)</param>
        /// <returns>An opened SqlConnection</returns>
        public async Task<SqlConnection> CreateAndOpenConnectionAsync(string preferredConnectionString = null)
        {
            var connection = CreateConnection(preferredConnectionString);
            
            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening SQL connection: {ErrorMessage}", ex.Message);
                
                // Dispose the connection if opening fails
                connection.Dispose();
                throw new ApplicationException("Failed to open database connection", ex);
            }
        }
    }

    /// <summary>
    /// Interface for SQL connection factory
    /// </summary>
    public interface ISqlConnectionFactory
    {
        SqlConnection CreateConnection(string preferredConnectionString = null);
        Task<SqlConnection> CreateAndOpenConnectionAsync(string preferredConnectionString = null);
    }
} 