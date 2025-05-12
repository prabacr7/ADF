using DataTransfer.Application.DTOs;
using DataTransfer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DataTransfer.API.Models;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConnectionController> _logger;
        private readonly IDataSourceService _dataSourceService;

        public ConnectionController(IConfiguration configuration, ILogger<ConnectionController> logger, IDataSourceService dataSourceService)
        {
            _configuration = configuration;
            _logger = logger;
            _dataSourceService = dataSourceService;
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestConnection([FromBody] ConnectionRequestDto request)
        {
            try
            {
                string connectionString = BuildConnectionString(request);
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to {Server}/{Database}", request.ServerName, request.Database);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("databases")]
        public async Task<IActionResult> GetDatabases([FromBody] ConnectionRequestDto request)
        {
            try
            {
                string connectionString = BuildConnectionString(request);
                var databases = new List<string>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Include all databases for SQL Server, including master (useful for querying system info)
                    using (var command = new SqlCommand("SELECT name FROM sys.databases ORDER BY name", connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                databases.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                return Ok(databases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting databases from {Server}", request.ServerName);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("tables")]
        public async Task<IActionResult> GetTables([FromBody] ConnectionRequestDto request)
        {
            try
            {
                // Log incoming request details (excluding password)
                _logger.LogInformation("Getting tables from {Server}/{Database}, Authentication={Auth}, HasUsername={HasUser}, HasPassword={HasPass}",
                    request.ServerName, 
                    request.Database, 
                    request.Authentication,
                    !string.IsNullOrEmpty(request.UserName),
                    !string.IsNullOrEmpty(request.Password));

                // Check if the request is using SQL authentication but missing credentials
                if (request.Authentication?.ToLower() == "sql" && string.IsNullOrEmpty(request.Password))
                {
                    // Try to get credentials from the data source service if userId is provided
                    if (request.UserId.HasValue)
                    {
                        try
                        {
                            var dataSources = await _dataSourceService.GetAllDataSourcesAsync(request.UserId);
                            var matchingDataSource = dataSources.FirstOrDefault(ds => 
                                ds.ServerName.Equals(request.ServerName, StringComparison.OrdinalIgnoreCase) &&
                                (ds.DefaultDatabaseName?.Equals(request.Database, StringComparison.OrdinalIgnoreCase) ?? false));
                            
                            if (matchingDataSource != null && !string.IsNullOrEmpty(matchingDataSource.Password))
                            {
                                _logger.LogInformation("Found stored credentials for {Server}/{Database}", request.ServerName, request.Database);
                                request.UserName = matchingDataSource.UserName;
                                request.Password = matchingDataSource.Password;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error retrieving stored credentials for {Server}/{Database}", request.ServerName, request.Database);
                        }
                    }
                }

                string connectionString = BuildConnectionString(request);
                var tables = new List<object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT 
                            s.name AS SchemaName,
                            t.name AS TableName,
                            QUOTENAME(s.name) + '.' + QUOTENAME(t.name) AS FullName,
                            OBJECT_SCHEMA_NAME(t.object_id) + '.' + t.name AS DisplayName
                        FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE t.is_ms_shipped = 0
                        ORDER BY s.name, t.name";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                tables.Add(new
                                {
                                    SchemaName = reader.GetString(0),
                                    TableName = reader.GetString(1),
                                    FullName = reader.GetString(2),
                                    DisplayName = reader.GetString(3)
                                });
                            }
                        }
                    }
                }

                if (!tables.Any())
                {
                    _logger.LogWarning("No tables found in database {Database} on server {Server}", 
                        request.Database, request.ServerName);
                }

                return Ok(new { 
                    success = true,
                    data = tables,
                    totalCount = tables.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables from {Server}/{Database}", request.ServerName, request.Database);
                return StatusCode(500, new { 
                    success = false,
                    message = ex.Message,
                    error = ex.ToString()
                });
            }
        }

        [HttpPost("query")]
        public async Task<IActionResult> ExecuteQuery([FromBody] DataTransfer.API.Models.QueryRequestDto request)
        {
            try
            {
                // Log incoming request details (excluding password)
                _logger.LogInformation("Executing query on {Server}/{Database}, Authentication={Auth}, HasUsername={HasUser}, HasPassword={HasPass}",
                    request.ServerName, 
                    request.Database, 
                    request.Authentication,
                    !string.IsNullOrEmpty(request.UserName),
                    !string.IsNullOrEmpty(request.Password));

                // Check if the request is using SQL authentication but missing credentials
                if (request.Authentication?.ToLower() == "sql" && string.IsNullOrEmpty(request.Password))
                {
                    // Try to get credentials from the data source service if userId is provided
                    if (request.UserId.HasValue)
                    {
                        try
                        {
                            var dataSources = await _dataSourceService.GetAllDataSourcesAsync(request.UserId);
                            var matchingDataSource = dataSources.FirstOrDefault(ds => 
                                ds.ServerName.Equals(request.ServerName, StringComparison.OrdinalIgnoreCase) &&
                                (ds.DefaultDatabaseName?.Equals(request.Database, StringComparison.OrdinalIgnoreCase) ?? false));
                            
                            if (matchingDataSource != null && !string.IsNullOrEmpty(matchingDataSource.Password))
                            {
                                _logger.LogInformation("Found stored credentials for {Server}/{Database}", request.ServerName, request.Database);
                                request.UserName = matchingDataSource.UserName;
                                request.Password = matchingDataSource.Password;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error retrieving stored credentials for {Server}/{Database}", request.ServerName, request.Database);
                        }
                    }
                }

                string connectionString = BuildConnectionString(request);
                var results = new List<object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(request.Query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                results.Add(row);
                            }
                        }
                    }
                }

                return Ok(new QueryResultDto<object>
                {
                    Data = results,
                    TotalCount = results.Count,
                    PageIndex = 0,
                    PageSize = results.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query on {Server}/{Database}: {Query}", 
                    request.ServerName, request.Database, request.Query);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("table-data")]
        public async Task<IActionResult> GetTableData([FromBody] TableDataRequestDto request)
        {
            try
            {
                // Log incoming request details (excluding password)
                _logger.LogInformation("Getting table data from {Server}/{Database}/{Table}, Authentication={Auth}, HasUsername={HasUser}, HasPassword={HasPass}",
                    request.ServerName, 
                    request.Database, 
                    request.TableName,
                    request.Authentication,
                    !string.IsNullOrEmpty(request.UserName),
                    !string.IsNullOrEmpty(request.Password));

                // Check if the request is using SQL authentication but missing credentials
                if (request.Authentication?.ToLower() == "sql" && string.IsNullOrEmpty(request.Password))
                {
                    // Try to get credentials from the data source service if userId is provided
                    if (request.UserId.HasValue)
                    {
                        try
                        {
                            var dataSources = await _dataSourceService.GetAllDataSourcesAsync(request.UserId);
                            var matchingDataSource = dataSources.FirstOrDefault(ds => 
                                ds.ServerName.Equals(request.ServerName, StringComparison.OrdinalIgnoreCase) &&
                                (ds.DefaultDatabaseName?.Equals(request.Database, StringComparison.OrdinalIgnoreCase) ?? false));
                            
                            if (matchingDataSource != null && !string.IsNullOrEmpty(matchingDataSource.Password))
                            {
                                _logger.LogInformation("Found stored credentials for {Server}/{Database}", request.ServerName, request.Database);
                                request.UserName = matchingDataSource.UserName;
                                request.Password = matchingDataSource.Password;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error retrieving stored credentials for {Server}/{Database}", request.ServerName, request.Database);
                        }
                    }
                }

                string connectionString = BuildConnectionString(request);
                var result = new QueryResultDto<Dictionary<string, object>>
                {
                    PageIndex = request.PageIndex,
                    PageSize = request.PageSize,
                    Data = new List<Dictionary<string, object>>()
                };

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // First, get the total count for pagination
                    string countQuery = $"SELECT COUNT(*) FROM [{request.TableName}]";
                    using (var command = new SqlCommand(countQuery, connection))
                    {
                        result.TotalCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }

                    // Then, get the data for the current page
                    string dataQuery = $@"
                        SELECT *
                        FROM [{request.TableName}]
                        ORDER BY (SELECT NULL)
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    using (var command = new SqlCommand(dataQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Offset", request.PageIndex * request.PageSize);
                        command.Parameters.AddWithValue("@PageSize", request.PageSize);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Get column names
                            var columns = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columns.Add(reader.GetName(i));
                            }

                            // Read data
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                result.Data.Add(row);
                            }
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from table {TableName} in {Database}", 
                    request.TableName, request.Database);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("query-data")]
        public async Task<IActionResult> ExecuteQueryWithPagination([FromBody] QueryDataRequestDto request)
        {
            try
            {
                string connectionString = BuildConnectionString(request);

                // Validate query for security (basic check)
                string query = request.Query.Trim();
                if (!query.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Only SELECT queries are allowed" });
                }

                // Check if this is a database name query (special case)
                bool isDatabaseNameQuery = query.ToUpper().Contains("SYS.DATABASES") &&
                                           (query.ToUpper().Contains("NAME") || query.ToUpper().Contains("DATABASENAME"));

                if (isDatabaseNameQuery)
                {
                    _logger.LogInformation("Database name query detected, executing without pagination");

                    // For database name queries, execute directly without pagination
                    var dbResult = new QueryResultDto<Dictionary<string, object>>
                    {
                        PageIndex = 0,
                        PageSize = 1000, // Large page size for all results
                        Data = new List<Dictionary<string, object>>()
                    };

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.CommandTimeout = 60; // 60 seconds timeout

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                // Get column names
                                var columns = new List<string>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    columns.Add(reader.GetName(i));
                                }

                                // Read data
                                while (await reader.ReadAsync())
                                {
                                    var row = new Dictionary<string, object>();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    }
                                    dbResult.Data.Add(row);
                                }
                            }
                        }
                    }

                    // Set total count to actual number of rows returned
                    dbResult.TotalCount = dbResult.Data.Count;

                    return Ok(dbResult);
                }

                // If no special case is handled, return a default response
                return BadRequest(new { message = "Unsupported query type or missing implementation." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query on {Server}/{Database}: {Query}",
                    request.ServerName, request.Database, request.Query);
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpPost("grid-data")]
        public async Task<IActionResult> GetGridData([FromBody] GridDataRequestDto request)
        {
            try
            {
                string connectionString = BuildConnectionString(request);

                // Calculate the number of rows to retrieve
                int rowsToRetrieve = request.EndRow - request.StartRow;
                
                var result = new GridDataResultDto<Dictionary<string, object>>
                {
                    StartRow = request.StartRow,
                    EndRow = request.EndRow,
                    Data = new List<Dictionary<string, object>>()
                };

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // First, get the total count
                    string countQuery = $"SELECT COUNT(*) FROM [{request.TableName}]";
                    using (var command = new SqlCommand(countQuery, connection))
                    {
                        result.TotalCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }

                    // Build the main query with sorting
                    string dataQuery = $"SELECT * FROM [{request.TableName}]";
                    
                    // Add sorting if provided
                    if (request.SortModel != null && request.SortModel.Count > 0)
                    {
                        var sortClauses = new List<string>();
                        foreach (var sort in request.SortModel)
                        {
                            // Safely escape column name to prevent SQL injection
                            string columnName = sort.ColId.Replace("]", "]]");
                            string direction = sort.Sort.ToUpper() == "DESC" ? "DESC" : "ASC";
                            sortClauses.Add($"[{columnName}] {direction}");
                        }
                        
                        if (sortClauses.Count > 0)
                        {
                            dataQuery += " ORDER BY " + string.Join(", ", sortClauses);
                        }
                        else
                        {
                            // Default ordering if no sort model is provided
                            dataQuery += " ORDER BY (SELECT NULL)";
                        }
                    }
                    else
                    {
                        // Default ordering if no sort model is provided
                        dataQuery += " ORDER BY (SELECT NULL)";
                    }

                    // Add pagination
                    dataQuery += $" OFFSET {request.StartRow} ROWS FETCH NEXT {rowsToRetrieve} ROWS ONLY";

                    using (var command = new SqlCommand(dataQuery, connection))
                    {
                        command.CommandTimeout = 60; // 60 seconds timeout

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Get column names
                            var columns = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columns.Add(reader.GetName(i));
                            }

                            // Read data
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                result.Data.Add(row);
                            }
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching grid data from table {TableName} in {Database}", 
                    request.TableName, request.Database);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("grid-query")]
        public async Task<IActionResult> ExecuteGridQuery([FromBody] GridQueryRequestDto request)
        {
            try
            {
                string connectionString = BuildConnectionString(request);

                // Calculate the number of rows to retrieve
                int rowsToRetrieve = request.EndRow - request.StartRow;
                
                // Validate query for security (basic check)
                string query = request.Query.Trim();
                if (!query.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Only SELECT queries are allowed" });
                }

                var result = new GridDataResultDto<Dictionary<string, object>>
                {
                    StartRow = request.StartRow,
                    EndRow = request.EndRow,
                    Data = new List<Dictionary<string, object>>()
                };

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    // First, get the total count
                    string countQuery = $"SELECT COUNT(*) FROM ({query}) AS CountQuery";
                    using (var command = new SqlCommand(countQuery, connection))
                    {
                        command.CommandTimeout = 60; // 60 seconds timeout
                        try
                        {
                            result.TotalCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                        }
                        catch (SqlException)
                        {
                            // If count query fails (e.g., with complex queries), we'll set -1 to indicate unknown total
                            result.TotalCount = -1;
                        }
                    }

                    // Build the main query with sorting and pagination
                    string paginatedQuery;
                    
                    // Check if the query already has an ORDER BY clause
                    bool hasOrderBy = query.ToUpper().Contains("ORDER BY");
                    
                    if (hasOrderBy)
                    {
                        // For queries with ORDER BY, use a different approach to avoid SQL errors
                        // Wrap the original query in a CTE and apply TOP/OFFSET
                        paginatedQuery = $@"
                            ;WITH OriginalQuery AS (
                                {query}
                            )
                            SELECT * FROM OriginalQuery
                            ORDER BY (SELECT NULL)
                            OFFSET {request.StartRow} ROWS FETCH NEXT {rowsToRetrieve} ROWS ONLY";
                    }
                    else
                    {
                        // Add sorting if provided and no existing ORDER BY
                        paginatedQuery = query;
                        
                        if (request.SortModel != null && request.SortModel.Count > 0)
                        {
                            var sortClauses = new List<string>();
                            foreach (var sort in request.SortModel)
                            {
                                // Safely escape column name to prevent SQL injection
                                string columnName = sort.ColId.Replace("]", "]]");
                                string direction = sort.Sort.ToUpper() == "DESC" ? "DESC" : "ASC";
                                sortClauses.Add($"[{columnName}] {direction}");
                            }
                            
                            if (sortClauses.Count > 0)
                            {
                                paginatedQuery += " ORDER BY " + string.Join(", ", sortClauses);
                            }
                            else
                            {
                                // Default ordering if no sort model is provided
                                paginatedQuery += " ORDER BY (SELECT NULL)";
                            }
                        }
                        else
                        {
                            // Default ordering if no sort model is provided
                            paginatedQuery += " ORDER BY (SELECT NULL)";
                        }
                        
                        // Add pagination
                        paginatedQuery += $" OFFSET {request.StartRow} ROWS FETCH NEXT {rowsToRetrieve} ROWS ONLY";
                    }

                    using (var command = new SqlCommand(paginatedQuery, connection))
                    {
                        command.CommandTimeout = 60; // 60 seconds timeout

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Get column names
                            var columns = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columns.Add(reader.GetName(i));
                            }

                            // Read data
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                result.Data.Add(row);
                            }
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing grid query on {Server}/{Database}: {Query}", 
                    request.ServerName, request.Database, request.Query);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("system-databases")]
        public async Task<IActionResult> GetSystemDatabases([FromBody] DatabaseFilterDto request)
        {
            try
            {
                _logger.LogInformation("Getting system databases from server: {Server} with filters", request.ServerName);
                
                // Override database to master for system catalog queries
                request.Database = "master";
                
                string connectionString = BuildConnectionString(request);
                var databases = new List<object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Base query
                    var queryBuilder = new StringBuilder();
                    queryBuilder.Append(@"
                        SELECT 
                            database_id AS DatabaseId,
                            name AS DatabaseName,
                            state_desc AS State,
                            compatibility_level AS CompatibilityLevel,
                            create_date AS CreateDate,
                            is_read_only AS IsReadOnly
                        FROM sys.databases
                        WHERE 1=1");
                    
                    // Apply filters
                    if (request.ExcludeSystemDatabases == true)
                    {
                        queryBuilder.Append(" AND database_id > 4"); // Exclude system DBs (master, tempdb, model, msdb)
                    }
                    
                    if (request.OnlyOnlineDatabases == true)
                    {
                        queryBuilder.Append(" AND state = 0"); // Only ONLINE databases
                    }
                    
                    if (request.MinimumCompatibilityLevel.HasValue)
                    {
                        queryBuilder.Append($" AND compatibility_level >= {request.MinimumCompatibilityLevel.Value}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(request.NameFilter))
                    {
                        // Escape single quotes for SQL
                        string safeNameFilter = request.NameFilter.Replace("'", "''");
                        queryBuilder.Append($" AND name LIKE '%{safeNameFilter}%'");
                    }
                    
                    // Add ordering
                    queryBuilder.Append(" ORDER BY name");
                    
                    string query = queryBuilder.ToString();
                    _logger.LogDebug("Executing database query: {Query}", query);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 30; // 30 seconds timeout
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                databases.Add(new
                                {
                                    DatabaseId = reader.GetInt32(0),
                                    DatabaseName = reader.GetString(1),
                                    State = reader.GetString(2),
                                    CompatibilityLevel = reader.GetInt32(3),
                                    CreateDate = reader.GetDateTime(4),
                                    IsReadOnly = reader.GetBoolean(5)
                                });
                            }
                        }
                    }
                }

                return Ok(new { 
                    success = true,
                    data = databases,
                    totalCount = databases.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system databases from {Server}", request.ServerName);
                return StatusCode(500, new { 
                    success = false,
                    message = $"Error getting system databases: {ex.Message}"
                });
            }
        }

        [HttpPost("database-names")]
        public async Task<IActionResult> GetDatabaseNames([FromBody] DatabaseFilterDto request)
        {
            try
            {
                _logger.LogInformation("Getting database names from server: {Server}", request.ServerName);
                
                // Override database to master for system catalog queries
                request.Database = "master";
                
                string connectionString = BuildConnectionString(request);
                var databaseNames = new List<string>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Build query with filters
                    var queryBuilder = new StringBuilder();
                    queryBuilder.Append("SELECT name FROM sys.databases WHERE 1=1");
                    
                    // Apply filters
                    if (request.ExcludeSystemDatabases == true)
                    {
                        queryBuilder.Append(" AND database_id > 4"); // Exclude system DBs
                    }
                    
                    if (request.OnlyOnlineDatabases == true)
                    {
                        queryBuilder.Append(" AND state = 0"); // Only ONLINE databases
                    }
                    
                    if (request.MinimumCompatibilityLevel.HasValue)
                    {
                        queryBuilder.Append($" AND compatibility_level >= {request.MinimumCompatibilityLevel.Value}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(request.NameFilter))
                    {
                        // Escape single quotes for SQL
                        string safeNameFilter = request.NameFilter.Replace("'", "''");
                        queryBuilder.Append($" AND name LIKE '%{safeNameFilter}%'");
                    }
                    
                    // Add ordering
                    queryBuilder.Append(" ORDER BY name");
                    
                    string query = queryBuilder.ToString();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 30; // 30 seconds timeout
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                databaseNames.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                return Ok(databaseNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database names from {Server}", request.ServerName);
                return StatusCode(500, new { 
                    success = false,
                    message = $"Error getting database names: {ex.Message}"
                });
            }
        }

        [HttpPost("database-sizes")]
        public async Task<IActionResult> GetDatabaseSizes([FromBody] DatabaseFilterDto request)
        {
            try
            {
                _logger.LogInformation("Getting database sizes from server: {Server}", request.ServerName);
                
                // Override database to master for system catalog queries
                request.Database = "master";
                
                string connectionString = BuildConnectionString(request);
                var databaseSizes = new List<object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Query to get database sizes using system catalog views
                    var query = @"
                        SELECT 
                            DB_NAME(database_id) AS DatabaseName,
                            CAST(ROUND((SUM(size) * 8.0 / 1024), 2) AS FLOAT) AS SizeMB,
                            CAST(ROUND((SUM(CASE WHEN type_desc = 'ROWS' THEN size ELSE 0 END) * 8.0 / 1024), 2) AS FLOAT) AS DataSizeMB,
                            CAST(ROUND((SUM(CASE WHEN type_desc = 'LOG' THEN size ELSE 0 END) * 8.0 / 1024), 2) AS FLOAT) AS LogSizeMB
                        FROM sys.master_files
                        WHERE database_id > 0 " + 
                        (request.ExcludeSystemDatabases == true ? "AND database_id > 4 " : "") +
                        @"GROUP BY database_id
                        ORDER BY SUM(size) DESC";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.CommandTimeout = 60; // 60 seconds timeout for larger servers
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    databaseSizes.Add(new
                                    {
                                        DatabaseName = reader.GetString(0),
                                        SizeMB = reader.GetDouble(1),
                                        DataSizeMB = reader.GetDouble(2),
                                        LogSizeMB = reader.GetDouble(3),
                                        SizeGB = Math.Round(reader.GetDouble(1) / 1024, 2)
                                    });
                                }
                            }
                        }
                }

                return Ok(new { 
                    success = true,
                    data = databaseSizes,
                    totalCount = databaseSizes.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database sizes from {Server}", request.ServerName);
                return StatusCode(500, new { 
                    success = false,
                    message = $"Error getting database sizes: {ex.Message}"
                });
            }
        }

        private string BuildConnectionString(ConnectionRequestDto request)
        {
            // Log connection parameters for debugging
            _logger.LogDebug("Building connection string with parameters: Server={Server}, Database={Database}, Authentication={Auth}, UserName={UserName}, HasPassword={HasPassword}",
                request.ServerName,
                request.Database,
                request.Authentication,
                request.UserName,
                !string.IsNullOrEmpty(request.Password));

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.ServerName,
                InitialCatalog = request.Database,
                IntegratedSecurity = request.Authentication?.ToLower() == "windows"
            };

            if (request.Authentication?.ToLower() == "sql")
            {
                builder.UserID = request.UserName ?? string.Empty;
                builder.Password = request.Password ?? string.Empty;
                
                // Additional debug logging for SQL authentication
                _logger.LogDebug("Using SQL Server authentication with UserID={UserID}, PasswordLength={PasswordLength}",
                    builder.UserID,
                    builder.Password?.Length ?? 0);
            }
            else
            {
                // Debug logging for Windows authentication
                _logger.LogDebug("Using Windows authentication (Integrated Security)");
            }

            builder.TrustServerCertificate = true;
            builder.MultipleActiveResultSets = true;
            builder.ApplicationName = "DataTransfer";

            return builder.ConnectionString;
        }
    }
} 