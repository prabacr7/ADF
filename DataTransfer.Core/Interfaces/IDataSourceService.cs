using DataTransfer.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface IDataSourceService
    {
        Task<bool> TestConnectionAsync(string serverName, string userName, string password, string authenticationType, string defaultDatabaseName);
        Task<DataSource> SaveDataSourceAsync(DataSource dataSource);
        Task<DataSource> GetDataSourceByIdAsync(int id);
        Task<IEnumerable<DataSource>> GetAllDataSourcesAsync(int? userId = null);
        Task<bool> UpdateDataSourceAsync(DataSource dataSource);
        Task<bool> DeleteDataSourceAsync(int id);
    }
} 