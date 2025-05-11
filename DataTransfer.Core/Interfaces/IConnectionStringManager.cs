using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace DataTransfer.Core.Interfaces
{
    public interface IConnectionStringManager
    {
        /// <summary>
        /// Creates a SqlConnection for the source database based on ImportData and DataSource information
        /// </summary>
        /// <param name="importDataId">The ID of the import job</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A configured SqlConnection for the source database</returns>
        Task<SqlConnection> CreateSourceConnectionAsync(int importDataId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a SqlConnection for the destination database based on ImportData and DataSource information
        /// </summary>
        /// <param name="importDataId">The ID of the import job</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A configured SqlConnection for the destination database</returns>
        Task<SqlConnection> CreateDestinationConnectionAsync(int importDataId, CancellationToken cancellationToken = default);
    }
} 