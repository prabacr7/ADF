using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Repositories
{
    public class ImportDataRepository : IImportDataRepository
    {
        private readonly string _connectionString;

        public ImportDataRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<ImportData> GetImportDataWithSourcesAsync(int importId, CancellationToken cancellationToken = default)
        {
            ImportData importData = null;
            
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                
                // First query to get the import data
                using (var command = new SqlCommand(
                    @"SELECT i.Id AS ImportId, 'ImportData' As Name, i.FromConnectionId ASFromDataSourceId, i.ToConnectionId As ToDataSourceId, 
                      i.FromTableName As FromTable, i.ToTableName As ToTable, i.Query, i.SourceColumnList  As FromColumnList, 
                      i.SourceColumnList as ToColumnList, i.ManText AS MappedColumnList, i.BeforeQuery, i.AfterQuert AS AfterQuery, 
                      i.IsTruncate, i.IsDeleteAndInsert AS IsDelete, i.CreatedDate, 1 AS IsActive
                      FROM ImportData i
					  Join DataSource D on d.DataSourceId=i.FromConnectionId ANd d.DataSourceId=i.ToConnectionId
                      WHERE i.Id == @ImportId", connection))
                {
                    command.Parameters.AddWithValue("@ImportId", importId);

                    using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        importData = new ImportData
                        {
                            ImportId = reader.GetInt32(reader.GetOrdinal("ImportId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            FromDataSourceId = reader.GetInt32(reader.GetOrdinal("FromDataSourceId")),
                            ToDataSourceId = reader.GetInt32(reader.GetOrdinal("ToDataSourceId")),
                            FromTable = reader.GetString(reader.GetOrdinal("FromTable")),
                            ToTable = reader.GetString(reader.GetOrdinal("ToTable")),
                            Query = reader.IsDBNull(reader.GetOrdinal("Query")) ? string.Empty : reader.GetString(reader.GetOrdinal("Query")),
                            FromColumnList = reader.GetString(reader.GetOrdinal("FromColumnList")),
                            ToColumnList = reader.GetString(reader.GetOrdinal("ToColumnList")),
                            MappedColumnList = reader.IsDBNull(reader.GetOrdinal("MappedColumnList")) ? string.Empty : reader.GetString(reader.GetOrdinal("MappedColumnList")),
                            BeforeQuery = reader.IsDBNull(reader.GetOrdinal("BeforeQuery")) ? string.Empty : reader.GetString(reader.GetOrdinal("BeforeQuery")),
                            AfterQuery = reader.IsDBNull(reader.GetOrdinal("AfterQuery")) ? string.Empty : reader.GetString(reader.GetOrdinal("AfterQuery")),
                            IsTruncate = reader.GetBoolean(reader.GetOrdinal("IsTruncate")),
                            IsDelete = reader.GetBoolean(reader.GetOrdinal("IsDelete")),
                            CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                        };
                    }
                }

                if (importData != null)
                {
                    // Second query to get the source data source
                    using (var command = new SqlCommand(
                        @"SELECT DataSourceId, DatasourceName, ServerName, UserName, Password, 
                          AuthenticationType, DefaultDatabaseName, UserId, CreatedDate, IsActive
                          FROM DataSource
                          WHERE DataSourceId = @DataSourceId", connection))
                    {
                        command.Parameters.AddWithValue("@DataSourceId", importData.FromDataSourceId);

                        using var reader = await command.ExecuteReaderAsync(cancellationToken);
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            importData.FromDataSource = new DataSource
                            {
                                DataSourceId = reader.GetInt32(reader.GetOrdinal("DataSourceId")),
                                DatasourceName = reader.GetString(reader.GetOrdinal("DatasourceName")),
                                ServerName = reader.GetString(reader.GetOrdinal("ServerName")),
                                UserName = reader.GetString(reader.GetOrdinal("UserName")),
                                Password = reader.GetString(reader.GetOrdinal("Password")),
                                AuthenticationType = reader.GetString(reader.GetOrdinal("AuthenticationType")),
                                DefaultDatabaseName = reader.GetString(reader.GetOrdinal("DefaultDatabaseName")),
                                UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("UserId")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            };
                        }
                    }

                    // Third query to get the destination data source
                    using (var command = new SqlCommand(
                        @"SELECT DataSourceId, DatasourceName, ServerName, UserName, Password, 
                          AuthenticationType, DefaultDatabaseName, UserId, CreatedDate, IsActive
                          FROM DataSource
                          WHERE DataSourceId = @DataSourceId", connection))
                    {
                        command.Parameters.AddWithValue("@DataSourceId", importData.ToDataSourceId);

                        using var reader = await command.ExecuteReaderAsync(cancellationToken);
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            importData.ToDataSource = new DataSource
                            {
                                DataSourceId = reader.GetInt32(reader.GetOrdinal("DataSourceId")),
                                DatasourceName = reader.GetString(reader.GetOrdinal("DatasourceName")),
                                ServerName = reader.GetString(reader.GetOrdinal("ServerName")),
                                UserName = reader.GetString(reader.GetOrdinal("UserName")),
                                Password = reader.GetString(reader.GetOrdinal("Password")),
                                AuthenticationType = reader.GetString(reader.GetOrdinal("AuthenticationType")),
                                DefaultDatabaseName = reader.GetString(reader.GetOrdinal("DefaultDatabaseName")),
                                UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("UserId")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            };
                        }
                    }
                }
            }

            return importData;
        }
    }
} 