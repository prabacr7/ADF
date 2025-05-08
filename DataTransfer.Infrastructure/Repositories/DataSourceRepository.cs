using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using DataTransfer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Repositories
{
    public class DataSourceRepository : IDataSourceRepository
    {
        private readonly ApplicationDbContext _context;

        public DataSourceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataSource> GetByIdAsync(int id)
        {
            return await _context.DataSources
                .FirstOrDefaultAsync(ds => ds.DataSourceId == id);
        }

        public async Task<IEnumerable<DataSource>> GetAllAsync(int? userId = null)
        {
            var query = _context.DataSources.AsQueryable();
            
            if (userId.HasValue)
            {
                query = query.Where(ds => ds.UserId == userId.Value);
            }
            
            return await query
                .OrderByDescending(ds => ds.CreatedDate)
                .ToListAsync();
        }

        public async Task<DataSource> AddAsync(DataSource dataSource)
        {
            await _context.DataSources.AddAsync(dataSource);
            await _context.SaveChangesAsync();
            return dataSource;
        }

        public async Task<bool> UpdateAsync(DataSource dataSource)
        {
            _context.DataSources.Update(dataSource);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var dataSource = await GetByIdAsync(id);
            if (dataSource == null)
                return false;
                
            _context.DataSources.Remove(dataSource);
            return await _context.SaveChangesAsync() > 0;
        }
    }
} 