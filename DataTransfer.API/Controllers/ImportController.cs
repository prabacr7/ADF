using DataTransfer.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Dapper;
using Cronos;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImportController> _logger;

        public ImportController(IConfiguration configuration, ILogger<ImportController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveImportData([FromBody] ImportDataDto request)
        {
            try
            {
                _logger.LogInformation("Saving import data for table {FromTable} to {ToTable}", 
                    request.FromTableName, request.ToTableName);

                // Validate required fields
                if (string.IsNullOrEmpty(request.FromDataBase) || 
                    string.IsNullOrEmpty(request.ToDataBase) ||
                    string.IsNullOrEmpty(request.FromTableName) ||
                    string.IsNullOrEmpty(request.ToTableName))
                {
                    return BadRequest(new ImportDataResponseDto
                    {
                        Success = false,
                        Message = "Required fields are missing"
                    });
                }

                // Set default datetime if not provided
                if (!request.CreatedDate.HasValue)
                {
                    request.CreatedDate = DateTime.Now;
                }

                // Calculate NextRunDateTime if CronJob is provided
                DateTime? nextRunDateTime = null;
                if (!string.IsNullOrWhiteSpace(request.CronJob))
                {
                    try
                    {
                        // Validate and parse the cron expression
                        var cronExpression = CronExpression.Parse(request.CronJob);
                        
                        // Calculate the next occurrence from current UTC time
                        var utcNow = DateTime.UtcNow;
                        var nextOccurrence = cronExpression.GetNextOccurrence(utcNow);
                        
                        if (nextOccurrence.HasValue)
                        {
                            nextRunDateTime = nextOccurrence.Value;
                            _logger.LogInformation("Calculated next run time: {NextRunTime} for cron expression: {CronExpression}", 
                                nextRunDateTime, request.CronJob);
                        }
                        else
                        {
                            _logger.LogWarning("No future occurrences found for cron expression: {CronExpression}", request.CronJob);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Invalid cron expression: {CronExpression}", request.CronJob);
                        return BadRequest(new ImportDataResponseDto
                        {
                            Success = false,
                            Message = $"Invalid cron expression: {request.CronJob}. The correct format is '* * * * *' (minute hour day month weekday)."
                        });
                    }
                }

                // Get the connection string from configuration with fallbacks
                string connectionString = null;
                string connectionStringName = null;

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

                // If no connection string found in configuration, use a fallback (but log a warning)
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Database=ISQLHelper;Trusted_Connection=True;TrustServerCertificate=True;";
                    _logger.LogWarning("No connection string found in configuration. Using default local SQL Server connection string.");
                }
                else
                {
                    _logger.LogInformation("Using connection string: {ConnectionStringName}", connectionStringName);
                }

                int importId;

                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        // Open connection with timeout
                        await connection.OpenAsync();

                        // First check if NextRunDateTime column exists
                        bool hasNextRunColumn = false;
                        try
                        {
                            using (var schemaCommand = new SqlCommand(
                                @"SELECT COUNT(*) 
                                FROM INFORMATION_SCHEMA.COLUMNS 
                                WHERE TABLE_NAME = 'ImportData' 
                                AND COLUMN_NAME = 'NextRunDateTime'", connection))
                            {
                                int count = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync());
                                hasNextRunColumn = (count > 0);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but continue - we can still insert without the columns
                            _logger.LogWarning(ex, "Could not verify or add NextRunDateTime/LastRunDateTime columns.");
                        }

                        // Prepare the INSERT query - include NextRunDateTime if we have the column
                        string insertQuery;
                        
                        if (hasNextRunColumn && nextRunDateTime.HasValue)
                        {
                            insertQuery = @"
                                INSERT INTO [dbo].[ImportData] (
                                    [UserId], [FromConnectionId], [ToConnectionId],
                                    [FromDataBase], [ToDataBase], [FromTableName], [ToTableName],
                                    [Query], [SourceColumnList], [DescColumnList], [ManText],
                                    [Description], [Istruncate], [IsDeleteAndInsert],
                                    [BeforeQuery], [AfterQuert], [CreatedDate], [CronJob],
                                    [NextRunDateTime], [LastRunDateTime]
                                ) VALUES (
                                    @UserId, @FromConnectionId, @ToConnectionId,
                                    @FromDataBase, @ToDataBase, @FromTableName, @ToTableName,
                                    @Query, @SourceColumnList, @DescColumnList, @ManText,
                                    @Description, @IsTruncate, @IsDeleteAndInsert,
                                    @BeforeQuery, @AfterQuery, @CreatedDate, @CronJob,
                                    @NextRunDateTime, NULL
                                );
                                SELECT CAST(SCOPE_IDENTITY() as int)";

                            // Execute the insert and get the new ID
                            importId = await connection.ExecuteScalarAsync<int>(insertQuery, new
                            {
                                request.UserId,
                                request.FromConnectionId,
                                request.ToConnectionId,
                                request.FromDataBase,
                                request.ToDataBase,
                                request.FromTableName,
                                request.ToTableName,
                                request.Query,
                                request.SourceColumnList,
                                request.DescColumnList,
                                request.ManText,
                                request.Description,
                                request.IsTruncate,
                                request.IsDeleteAndInsert,
                                request.BeforeQuery,
                                AfterQuery = request.AfterQuery,
                                request.CreatedDate,
                                request.CronJob,
                                NextRunDateTime = nextRunDateTime
                            });
                        }
                    }
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "SQL Error when connecting to database using connection string {ConnectionStringName}. Error: {ErrorMessage}. Number: {Number}", 
                        connectionStringName, sqlEx.Message, sqlEx.Number);
                    
                    string userFriendlyMessage;
                    
                    // Provide more specific error messages based on SQL exception number
                    switch (sqlEx.Number)
                    {
                        case 4060: // Invalid Database
                            userFriendlyMessage = "The specified database does not exist or is not accessible.";
                            break;
                        case 18456: // Login Failed
                            userFriendlyMessage = "Authentication failed - incorrect username or password.";
                            break;
                        case 2: // Timeout
                            userFriendlyMessage = "Connection timeout - the server did not respond in time.";
                            break;
                        case 53: // Server not found
                            userFriendlyMessage = "SQL Server instance was not found or is not accessible.";
                            break;
                        case 40: // Named Pipes error
                            userFriendlyMessage = "Could not connect to SQL Server - check server name and network connectivity.";
                            break;
                        default:
                            userFriendlyMessage = "A database error occurred while saving import data.";
                            break;
                    }
                    
                    return StatusCode(500, new ImportDataResponseDto
                    {
                        Success = false,
                        Message = $"Database connection error: {userFriendlyMessage}"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error when connecting to database using connection string {ConnectionStringName}. Error: {ErrorMessage}", 
                        connectionStringName, ex.Message);
                    
                    return StatusCode(500, new ImportDataResponseDto
                    {
                        Success = false,
                        Message = $"Unexpected error when saving import data: {ex.Message}"
                    });
                }

                _logger.LogInformation("Successfully saved import data with ID: {ImportId}", importId);

                return Ok(new ImportDataResponseDto
                {
                    Success = true,
                    Message = "Import data saved successfully",
                    ImportId = importId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveImportData endpoint: {ErrorMessage}", ex.Message);
                
                return StatusCode(500, new ImportDataResponseDto
                {
                    Success = false,
                    Message = $"Error saving import data: {ex.Message}"
                });
            }
        }
    }
} 