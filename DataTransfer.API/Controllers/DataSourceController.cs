using DataTransfer.Application.DTOs;
using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using DataTransfer.API.Models;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataSourceController : ControllerBase
    {
        private readonly IDataSourceService _dataSourceService;
        private readonly ILogger<DataSourceController> _logger;
        private readonly IDatabaseService _databaseService;

        public DataSourceController(
            IDataSourceService dataSourceService, 
            ILogger<DataSourceController> logger,
            IDatabaseService databaseService)
        {
            _dataSourceService = dataSourceService;
            _logger = logger;
            _databaseService = databaseService;
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection([FromBody] TestConnectionDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _dataSourceService.TestConnectionAsync(
                    model.ServerName,
                    model.UserName,
                    model.Password,
                    model.AuthenticationType,
                    model.DefaultDatabaseName);

                return Ok(new { success = result, message = result ? "Connection successful" : "Failed to connect to the database" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing database connection: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while testing the connection" });
            }
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveDataSource([FromBody] DataSourceDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logger.LogInformation("Saving data source with name: {DatasourceName}, server: {ServerName}", 
                    model.DatasourceName, model.ServerName);
                
                // Check if datasource with the same name already exists
                var existingSources = await _dataSourceService.GetAllDataSourcesAsync();
                var duplicateName = existingSources.FirstOrDefault(ds => 
                    string.Equals(ds.DatasourceName, model.DatasourceName, StringComparison.OrdinalIgnoreCase));
                
                if (duplicateName != null)
                {
                    _logger.LogWarning("Duplicate datasource name found: {DatasourceName}", model.DatasourceName);
                    return BadRequest(new { 
                        success = false, 
                        message = $"A data source with the name '{model.DatasourceName}' already exists. Please use a different name." 
                    });
                }
                
                var dataSource = new DataSource
                {
                    DatasourceName = model.DatasourceName,
                    ServerName = model.ServerName,
                    UserName = model.UserName,
                    Password = model.Password,
                    AuthenticationType = model.AuthenticationType,
                    DefaultDatabaseName = model.DefaultDatabaseName,
                    UserId = model.UserId ?? 1, // Default to 1 if not provided, in a real app this would be the current user
                    CreatedDate = DateTime.UtcNow
                    // IsActive is now ignored by EF Core
                };

                var result = await _dataSourceService.SaveDataSourceAsync(dataSource);

                _logger.LogInformation("Data source saved successfully with ID: {DataSourceId}", result.DataSourceId);
                
                return Ok(new { 
                    success = true, 
                    dataSourceId = result.DataSourceId,
                    message = "Data source saved successfully" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving data source: {Message}", ex.Message);
                
                // Return more detailed error info to help debug the issue
                string detailedMessage = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { success = false, message = $"An error occurred while saving the data source: {detailedMessage}" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DataSourceDto>>> GetAllDataSources([FromQuery] int? userId = null)
        {
            try
            {
                var dataSources = await _dataSourceService.GetAllDataSourcesAsync(userId);
                var result = new List<DataSourceDto>();

                foreach (var ds in dataSources)
                {
                    result.Add(new DataSourceDto
                    {
                        DataSourceId = ds.DataSourceId,
                        DatasourceName = ds.DatasourceName,
                        ServerName = ds.ServerName,
                        UserName = ds.UserName,
                        // Don't include the password in the response
                        AuthenticationType = ds.AuthenticationType,
                        DefaultDatabaseName = ds.DefaultDatabaseName,
                        UserId = ds.UserId,
                        CreatedDate = ds.CreatedDate,
                        IsActive = ds.IsActive
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data sources: {Message}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while retrieving data sources" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DataSourceDto>> GetDataSourceById(int id)
        {
            try
            {
                var dataSource = await _dataSourceService.GetDataSourceByIdAsync(id);

                if (dataSource == null)
                    return NotFound();

                var dto = new DataSourceDto
                {
                    DataSourceId = dataSource.DataSourceId,
                    DatasourceName = dataSource.DatasourceName,
                    ServerName = dataSource.ServerName,
                    UserName = dataSource.UserName,
                    // Include password for editing, it's already decrypted in the service
                    Password = dataSource.Password,
                    AuthenticationType = dataSource.AuthenticationType,
                    DefaultDatabaseName = dataSource.DefaultDatabaseName,
                    UserId = dataSource.UserId,
                    CreatedDate = dataSource.CreatedDate,
                    IsActive = dataSource.IsActive
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data source: {Message}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while retrieving the data source" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDataSource(int id, [FromBody] DataSourceDto model)
        {
            if (id != model.DataSourceId)
                return BadRequest("ID mismatch");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var existingDataSource = await _dataSourceService.GetDataSourceByIdAsync(id);

                if (existingDataSource == null)
                    return NotFound();
                
                // Check for duplicate name, but exclude the current source
                var allSources = await _dataSourceService.GetAllDataSourcesAsync();
                var duplicateName = allSources.FirstOrDefault(ds => 
                    ds.DataSourceId != id && 
                    string.Equals(ds.DatasourceName, model.DatasourceName, StringComparison.OrdinalIgnoreCase));
                
                if (duplicateName != null)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = $"A data source with the name '{model.DatasourceName}' already exists. Please use a different name." 
                    });
                }

                existingDataSource.DatasourceName = model.DatasourceName;
                existingDataSource.ServerName = model.ServerName;
                existingDataSource.UserName = model.UserName;
                
                // Only update password if provided
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    existingDataSource.Password = model.Password;
                }
                
                existingDataSource.AuthenticationType = model.AuthenticationType;
                existingDataSource.DefaultDatabaseName = model.DefaultDatabaseName;
                // IsActive is now ignored

                var result = await _dataSourceService.UpdateDataSourceAsync(existingDataSource);

                return Ok(new { success = result, message = result ? "Data source updated successfully" : "Failed to update data source" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data source: {Message}", ex.Message);
                string detailedMessage = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { success = false, message = $"An error occurred while updating the data source: {detailedMessage}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDataSource(int id)
        {
            try
            {
                var result = await _dataSourceService.DeleteDataSourceAsync(id);

                if (!result)
                    return NotFound();

                return Ok(new { success = true, message = "Data source deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data source: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the data source" });
            }
        }

        [HttpPost("execute-query")]
        public async Task<IActionResult> ExecuteQuery([FromBody] DataTransfer.API.Models.QueryRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logger.LogInformation("Executing query on server: {ServerName}, database: {Database}", 
                    request.ServerName, request.Database);

                // Build connection string
                string connectionString = BuildConnectionString(request);

                // Create database connection object
                var connection = new DatabaseConnection
                {
                    ConnectionString = connectionString,
                    ServerName = request.ServerName,
                    DatabaseName = request.Database
                };

                // Safety check - don't allow certain dangerous operations
                string normalizedQuery = request.Query.Trim().ToUpper();
                if (normalizedQuery.Contains("DROP DATABASE") || 
                    normalizedQuery.Contains("DROP TABLE") ||
                    normalizedQuery.Contains("TRUNCATE TABLE") ||
                    normalizedQuery.Contains("DELETE FROM") ||
                    normalizedQuery.Contains("UPDATE ") ||
                    normalizedQuery.Contains("INSERT INTO"))
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Data modification queries are not allowed for security reasons. Only SELECT statements are permitted." 
                    });
                }

                // Apply row limit if specified
                string query = request.Query;
                if (request.MaxRows.HasValue && !normalizedQuery.Contains("TOP"))
                {
                    // Simple implementation - will not work for complex queries
                    int maxRows = request.MaxRows.Value;
                    int selectIndex = query.ToUpper().IndexOf("SELECT");
                    if (selectIndex >= 0)
                    {
                        query = query.Insert(selectIndex + 6, $" TOP {maxRows} ");
                    }
                }

                // Execute the query
                var results = await _databaseService.QueryAsync(connection, query);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Message}", ex.Message);
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Error executing query: {ex.Message}" 
                });
            }
        }

        [HttpPost("get-tables")]
        public async Task<IActionResult> GetTables([FromBody] DataTransfer.API.Models.QueryRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logger.LogInformation("Getting tables from server: {ServerName}, database: {Database}", 
                    request.ServerName, request.Database);

                // Build connection string
                string connectionString = BuildConnectionString(request);

                // Create database connection object
                var connection = new DatabaseConnection
                {
                    ConnectionString = connectionString,
                    ServerName = request.ServerName,
                    DatabaseName = request.Database
                };

                // Get tables
                var tables = await _databaseService.GetTablesAsync(connection);

                // Convert to response format with schema and display name
                var tableList = tables.Select(t => {
                    string schemaName = "dbo";
                    string tableName = t;
                    
                    if (t.Contains("."))
                    {
                        var parts = t.Split('.');
                        schemaName = parts[0].Trim('[', ']');
                        tableName = parts[1].Trim('[', ']');
                    }
                    
                    return new {
                        schemaName = schemaName,
                        tableName = tableName,
                        fullName = t,
                        displayName = t
                    };
                }).ToList();

                return Ok(tableList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables: {Message}", ex.Message);
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Error getting tables: {ex.Message}" 
                });
            }
        }

        [HttpPost("get-databases")]
        public async Task<IActionResult> GetDatabases([FromBody] DataTransfer.API.Models.QueryRequestDto request)
        {
            if (string.IsNullOrEmpty(request.ServerName))
                return BadRequest(new { message = "Server name is required" });

            try
            {
                _logger.LogInformation("Getting databases from server: {ServerName}", request.ServerName);

                // Set master database for system queries
                request.Database = "master";

                // Build connection string
                string connectionString = BuildConnectionString(request);

                // Use direct SQL connection for this operation
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    await dbConnection.OpenAsync();

                    // Get all databases
                    var databases = await dbConnection.QueryAsync<string>(
                        "SELECT name FROM sys.databases ORDER BY name");

                    return Ok(databases);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting databases: {Message}", ex.Message);
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Error getting databases: {ex.Message}" 
                });
            }
        }

        private string BuildConnectionString(DataTransfer.API.Models.QueryRequestDto request)
        {
            // Log connection parameters for debugging
            _logger.LogDebug("Building connection string with parameters: Server={Server}, Database={Database}, Authentication={Auth}, UserName={UserName}, HasPassword={HasPassword}",
                request.ServerName,
                request.Database,
                request.Authentication,
                request.UserName,
                !string.IsNullOrEmpty(request.Password));

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = request.ServerName,
                InitialCatalog = request.Database,
                TrustServerCertificate = request.TrustServerCertificate
            };
            
            if (request.Authentication?.ToLower() == "sql")
            {
                builder.IntegratedSecurity = false;
                builder.UserID = request.UserName ?? string.Empty;
                builder.Password = request.Password ?? string.Empty;
                
                // Additional debug logging for SQL authentication
                _logger.LogDebug("Using SQL Server authentication with UserID={UserID}, PasswordLength={PasswordLength}",
                    builder.UserID,
                    builder.Password?.Length ?? 0);
            }
            else
            {
                builder.IntegratedSecurity = true;
                _logger.LogDebug("Using Windows authentication (Integrated Security)");
            }
            
            return builder.ConnectionString;
        }
    }
} 