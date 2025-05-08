using DataTransfer.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataTransfer.Core.Interfaces
{
    public interface IDataSourceRepository
    {
        Task<DataSource> GetByIdAsync(int id);
        Task<IEnumerable<DataSource>> GetAllAsync(int? userId = null);
        Task<DataSource> AddAsync(DataSource dataSource);
        Task<bool> UpdateAsync(DataSource dataSource);
        Task<bool> DeleteAsync(int id);
    }
} 