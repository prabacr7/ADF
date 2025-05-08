using DataTransfer.Core.Entities;

namespace DataTransfer.Core.Interfaces
{
    public interface IDatabaseService
    {
        Task<IEnumerable<string>> GetTablesAsync(DatabaseConnection connection);
        Task<TableInfo> GetTableInfoAsync(DatabaseConnection connection, string tableName);
        Task<int> ExecuteQueryAsync(DatabaseConnection connection, string query);
        Task<IEnumerable<dynamic>> QueryAsync(DatabaseConnection connection, string query);
    }
} 