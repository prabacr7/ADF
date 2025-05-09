using DataTransfer.Core.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface IImportExecutor
    {
        Task<bool> ExecuteImportAsync(ImportData importData, CancellationToken cancellationToken = default);
    }
} 