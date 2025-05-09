using DataTransfer.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface ISchedulerRepository
    {
        Task<IEnumerable<SchedulerEntry>> GetDueJobsAsync(DateTime currentTime, CancellationToken cancellationToken = default);
        Task UpdateLastExecutionTimeAsync(SchedulerEntry schedulerEntry, DateTime executionTime, CancellationToken cancellationToken = default);
        Task<SchedulerEntry> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    }
} 