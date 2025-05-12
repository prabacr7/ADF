using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Repositories
{
    public class SchedulerRepository : ISchedulerRepository
    {
        private readonly string _connectionString;

        public SchedulerRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<SchedulerEntry>> GetDueJobsAsync(DateTime currentTime, CancellationToken cancellationToken = default)
        {
            var dueJobs = new List<SchedulerEntry>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                
                using var command = new SqlCommand(
                    @"SELECT Id, Cron, LastUpdateDateTime, NextUpdateDatetime, ImportId, CreatedDate, IsActive 
                      FROM Scheduler 
                      WHERE NextUpdateDatetime <= @CurrentTime AND IsActive = 1", 
                    connection);
                
                command.Parameters.AddWithValue("@CurrentTime", currentTime);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    dueJobs.Add(new SchedulerEntry
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Cron = reader.IsDBNull(reader.GetOrdinal("Cron")) ? null : reader.GetString(reader.GetOrdinal("Cron")),
                        LastUpdateDateTime = reader.GetDateTime(reader.GetOrdinal("LastUpdateDateTime")),
                        NextUpdateDatetime = reader.GetDateTime(reader.GetOrdinal("NextUpdateDatetime")),
                        ImportId = reader.GetInt32(reader.GetOrdinal("ImportId")),
                        CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                    });
                }
            }

            return dueJobs;
        }

        public async Task UpdateLastExecutionTimeAsync(SchedulerEntry schedulerEntry, DateTime executionTime, CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(
                @"UPDATE Scheduler 
                  SET LastUpdateDateTime = @ExecutionTime,
                      NextUpdateDatetime = CASE 
                                             WHEN Cron IS NOT NULL THEN DATEADD(HOUR, 1, @ExecutionTime) 
                                             ELSE DATEADD(DAY, 1, @ExecutionTime) 
                                           END
                  WHERE Id = @Id", 
                connection);
                
            command.Parameters.AddWithValue("@ExecutionTime", executionTime);
            command.Parameters.AddWithValue("@Id", schedulerEntry.Id);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<SchedulerEntry> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(
                @"SELECT Id, Cron, LastUpdateDateTime, NextUpdateDatetime, ImportId, CreatedDate, IsActive 
                  FROM Scheduler 
                  WHERE Id = @Id", 
                connection);
                
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new SchedulerEntry
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Cron = reader.IsDBNull(reader.GetOrdinal("Cron")) ? null : reader.GetString(reader.GetOrdinal("Cron")),
                    LastUpdateDateTime = reader.GetDateTime(reader.GetOrdinal("LastUpdateDateTime")),
                    NextUpdateDatetime = reader.GetDateTime(reader.GetOrdinal("NextUpdateDatetime")),
                    ImportId = reader.GetInt32(reader.GetOrdinal("ImportId")),
                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                };
            }

            return null;
        }

        /// <summary>
        /// Gets all imports from ImportData table that have a non-null CronJob expression
        /// </summary>
        public async Task<IEnumerable<ImportData>> GetImportsWithCronJobAsync(CancellationToken cancellationToken = default)
        {
            var imports = new List<ImportData>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Check if LastRunDateTime column exists
                bool hasLastRunColumn = false;
                
                using (var schemaCommand = new SqlCommand(
                    @"SELECT COUNT(*) 
                      FROM INFORMATION_SCHEMA.COLUMNS 
                      WHERE TABLE_NAME = 'ImportData' 
                      AND COLUMN_NAME = 'LastRunDateTime'", connection))
                {
                    int count = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken));
                    hasLastRunColumn = (count > 0);
                }

                string query = hasLastRunColumn
                    ? @"SELECT Id AS ImportId, CronJob, LastRunDateTime, CreatedDate
                        FROM ImportData 
                        WHERE CronJob IS NOT NULL AND CronJob <> ''"
                    : @"SELECT Id AS ImportId, CronJob, CreatedDate
                        FROM ImportData 
                        WHERE CronJob IS NOT NULL AND CronJob <> ''";

                using var command = new SqlCommand(query, connection);
                
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var import = new ImportData
                    {
                        ImportId = reader.GetInt32(reader.GetOrdinal("ImportId")),
                        CronJob = reader.GetString(reader.GetOrdinal("CronJob")),
                        CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                    };

                    if (hasLastRunColumn && !reader.IsDBNull(reader.GetOrdinal("LastRunDateTime")))
                    {
                        import.LastRunDateTime = reader.GetDateTime(reader.GetOrdinal("LastRunDateTime"));
                    }

                    imports.Add(import);
                }
            }

            return imports;
        }

        /// <summary>
        /// Updates the LastRunDateTime field in ImportData table after a successful job execution
        /// </summary>
        public async Task UpdateImportLastRunTimeAsync(int importId, DateTime executionTime, CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if LastRunDateTime column exists
            bool hasLastRunColumn = false;
            
            using (var schemaCommand = new SqlCommand(
                @"SELECT COUNT(*) 
                  FROM INFORMATION_SCHEMA.COLUMNS 
                  WHERE TABLE_NAME = 'ImportData' 
                  AND COLUMN_NAME = 'LastRunDateTime'", connection))
            {
                int count = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken));
                hasLastRunColumn = (count > 0);
            }

            // If LastRunDateTime column doesn't exist, try to add it
            if (!hasLastRunColumn)
            {
                try
                {
                    using var alterCommand = new SqlCommand(
                        @"ALTER TABLE ImportData
                          ADD LastRunDateTime DATETIME NULL", connection);
                    
                    await alterCommand.ExecuteNonQueryAsync(cancellationToken);
                    hasLastRunColumn = true;
                }
                catch (Exception)
                {
                    // Column couldn't be added, possibly due to permissions
                    // Just continue without updating this field
                }
            }

            if (hasLastRunColumn)
            {
                // Update the LastRunDateTime
                using var updateCommand = new SqlCommand(
                    @"UPDATE ImportData 
                      SET LastRunDateTime = @ExecutionTime
                      WHERE Id = @ImportId", connection);
                    
                updateCommand.Parameters.AddWithValue("@ExecutionTime", executionTime);
                updateCommand.Parameters.AddWithValue("@ImportId", importId);

                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Adds a NextRunDateTime column to ImportData table if it doesn't exist
        /// </summary>
        public async Task<bool> EnsureNextRunDateTimeColumnExistsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if NextRunDateTime column exists
            bool hasNextRunColumn = false;
            
            using (var schemaCommand = new SqlCommand(
                @"SELECT COUNT(*) 
                  FROM INFORMATION_SCHEMA.COLUMNS 
                  WHERE TABLE_NAME = 'ImportData' 
                  AND COLUMN_NAME = 'NextRunDateTime'", connection))
            {
                int count = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken));
                hasNextRunColumn = (count > 0);
            }

            // If NextRunDateTime column doesn't exist, try to add it
            if (!hasNextRunColumn)
            {
                try
                {
                    using var alterCommand = new SqlCommand(
                        @"ALTER TABLE ImportData
                          ADD NextRunDateTime DATETIME NULL", connection);
                    
                    await alterCommand.ExecuteNonQueryAsync(cancellationToken);
                    return true;
                }
                catch (Exception)
                {
                    // Column couldn't be added, possibly due to permissions
                    return false;
                }
            }

            return hasNextRunColumn;
        }

        /// <summary>
        /// Updates the NextRunDateTime field in ImportData table to cache the next scheduled run
        /// </summary>
        public async Task UpdateNextRunTimeAsync(int importId, DateTime nextRunTime, CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if NextRunDateTime column exists
            bool hasNextRunColumn = false;
            
            using (var schemaCommand = new SqlCommand(
                @"SELECT COUNT(*) 
                  FROM INFORMATION_SCHEMA.COLUMNS 
                  WHERE TABLE_NAME = 'ImportData' 
                  AND COLUMN_NAME = 'NextRunDateTime'", connection))
            {
                int count = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken));
                hasNextRunColumn = (count > 0);
            }

            if (hasNextRunColumn)
            {
                // Update the NextRunDateTime
                using var updateCommand = new SqlCommand(
                    @"UPDATE ImportData 
                      SET NextRunDateTime = @NextRunTime
                      WHERE Id = @ImportId", connection);
                    
                updateCommand.Parameters.AddWithValue("@NextRunTime", nextRunTime);
                updateCommand.Parameters.AddWithValue("@ImportId", importId);

                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
} 