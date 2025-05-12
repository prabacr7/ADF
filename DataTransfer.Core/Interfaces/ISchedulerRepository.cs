using DataTransfer.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface ISchedulerRepository
    {
        // Legacy Scheduler table methods
        Task<IEnumerable<SchedulerEntry>> GetDueJobsAsync(DateTime currentTime, CancellationToken cancellationToken = default);
        Task UpdateLastExecutionTimeAsync(SchedulerEntry schedulerEntry, DateTime executionTime, CancellationToken cancellationToken = default);
        Task<SchedulerEntry> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        
        // New CronJob-based methods using ImportData table
        Task<IEnumerable<ImportData>> GetImportsWithCronJobAsync(CancellationToken cancellationToken = default);
        Task UpdateImportLastRunTimeAsync(int importId, DateTime executionTime, CancellationToken cancellationToken = default);
        Task<bool> EnsureNextRunDateTimeColumnExistsAsync(CancellationToken cancellationToken = default);
        Task UpdateNextRunTimeAsync(int importId, DateTime nextRunTime, CancellationToken cancellationToken = default);
    }
} 