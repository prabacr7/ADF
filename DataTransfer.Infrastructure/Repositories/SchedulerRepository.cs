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
    }
} 