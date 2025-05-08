using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using DataTransfer.API.Models;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TableDataController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TableDataController> _logger;

        public TableDataController(IConfiguration configuration, ILogger<TableDataController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("get-table-data")]
        public async Task<IActionResult> GetTableData([FromBody] GridDataRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Fetching data from table {request.TableName} with pagination {request.StartRow}-{request.EndRow}");

                var connectionString = BuildConnectionString(request);
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Get total count
                var countQuery = $"SELECT COUNT(*) FROM [{request.TableName}]";
                var totalCount = await connection.ExecuteScalarAsync<int>(countQuery);

                // Build the main query with pagination
                var query = $@"
                    SELECT *
                    FROM (
                        SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RowNum, *
                        FROM [{request.TableName}]
                    ) AS T
                    WHERE RowNum BETWEEN @StartRow AND @EndRow";

                var parameters = new DynamicParameters();
                parameters.Add("@StartRow", request.StartRow + 1); // SQL Server is 1-based
                parameters.Add("@EndRow", request.EndRow);

                var data = await connection.QueryAsync(query, parameters);
                
                return Ok(new GridDataResultDto<dynamic>
                {
                    Data = data.ToList(),
                    TotalCount = totalCount,
                    StartRow = request.StartRow,
                    EndRow = request.EndRow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching data from table {request.TableName}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string BuildConnectionString(ConnectionRequestDto request)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.ServerName,
                InitialCatalog = request.Database,
                IntegratedSecurity = request.Authentication == "windows"
            };

            if (request.Authentication == "sql")
            {
                builder.UserID = request.UserName;
                builder.Password = request.Password;
            }

            builder.TrustServerCertificate = true;
            return builder.ConnectionString;
        }
    }
} 