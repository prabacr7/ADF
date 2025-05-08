using DataTransfer.Core.Entities;

namespace DataTransfer.Core.Interfaces
{
    public interface IDataTransferService
    {
        Task<TransferResult> TransferDataAsync(TransferRequest request, IProgress<int>? progress = null);
        Task<IEnumerable<string>> GetSourceTablesAsync(DatabaseConnection connection);
        Task<IEnumerable<string>> GetDestinationTablesAsync(DatabaseConnection connection);
        Task<TableInfo> GetSourceTableInfoAsync(DatabaseConnection connection, string tableName);
        Task<TableInfo> GetDestinationTableInfoAsync(DatabaseConnection connection, string tableName);
    }
} 