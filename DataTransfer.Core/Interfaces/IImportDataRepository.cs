using DataTransfer.Core.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface IImportDataRepository
    {
        Task<ImportData> GetImportDataWithSourcesAsync(int importId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all import data records that have a non-null CronJob value
        /// </summary>
        Task<IEnumerable<ImportData>> GetImportsWithCronJobAsync(CancellationToken cancellationToken = default);
    }
} 