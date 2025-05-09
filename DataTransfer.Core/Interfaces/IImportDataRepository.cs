using DataTransfer.Core.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface IImportDataRepository
    {
        Task<ImportData> GetImportDataWithSourcesAsync(int importId, CancellationToken cancellationToken = default);
    }
} 