using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace DataTransfer.Infrastructure.Repositories
{
    public class ImportDataRepository : IImportDataRepository
    {
        private readonly string _connectionString;

        public ImportDataRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // Helper method to safely read boolean values
        private bool ReadBooleanSafe(SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return false; // Default for non-nullable bool, adjust if a nullable bool is preferred
            }

            object value = reader.GetValue(ordinal);

            if (value is bool boolVal) return boolVal;
            if (value is string stringVal)
            {
                if (bool.TryParse(stringVal, out var parsedBool)) return parsedBool; // Handles "True", "False"
                if (int.TryParse(stringVal, out var parsedInt)) return parsedInt != 0; // Handles "1", "0"
                // Consider throwing a more specific error or logging if string is not recognized
                throw new InvalidCastException($"Cannot convert string value '{stringVal}' for column {columnName} to Boolean.");
            }
            // Handle common numeric types that SQL Server might return for a BIT-like column or a '1' literal
            if (value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is decimal)
            {
                // Convert.ToBoolean can handle numeric types but ToInt32 is safer for comparison to 0
                return System.Convert.ToInt32(value) != 0;
            }
            
            throw new InvalidCastException($"Cannot convert value for column {columnName} of type {value.GetType()} to Boolean.");
        }

        public async Task<ImportData> GetImportDataWithSourcesAsync(int importId, CancellationToken cancellationToken = default)
        {
            ImportData importData = null;
            
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                
                // First check if the ImportData table has FromDatabase and ToDatabase columns
                bool hasFromToDbColumns = false;
                
                using (var schemaCommand = new SqlCommand(
                    @"SELECT COUNT(*) 
                      FROM INFORMATION_SCHEMA.COLUMNS 
                      WHERE TABLE_NAME = 'ImportData' 
                      AND COLUMN_NAME IN ('FromDatabase', 'ToDatabase')", connection))
                {
                    int count = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken));
                    hasFromToDbColumns = (count == 2); // Both columns exist
                }
                
                // First query to get the import data
                using (var command = new SqlCommand(
                    hasFromToDbColumns
                    ? @"SELECT i.Id AS ImportId, 
                             'ImportData' AS Name, 
                             i.FromConnectionId AS FromDataSourceId, 
                             i.ToConnectionId AS ToDataSourceId, 
                             i.FromTableName AS FromTable, 
                             i.ToTableName AS ToTable, 
                             i.FromDatabase AS FromDataBase,
                             i.ToDatabase AS ToDataBase,
                             i.Query, 
                             i.SourceColumnList AS FromColumnList, 
                             i.SourceColumnList AS ToColumnList, 
                             i.ManText AS MappedColumnList, 
                             i.BeforeQuery, 
                             i.AfterQuert AS AfterQuery, 
                             i.IsTruncate, 
                             i.IsDeleteAndInsert AS IsDelete, 
                             i.CreatedDate, 
                             1 AS IsActive
                      FROM ImportData i
                      JOIN DataSource d ON d.DataSourceId = i.FromConnectionId 
                      JOIN DataSource s ON s.DataSourceId = i.ToConnectionId 
                      WHERE i.Id = @ImportId"
                    : @"SELECT i.Id AS ImportId, 
                             'ImportData' AS Name, 
                             i.FromConnectionId AS FromDataSourceId, 
                             i.ToConnectionId AS ToDataSourceId, 
                             i.FromTableName AS FromTable, 
                             i.ToTableName AS ToTable, 
                             '' AS FromDataBase,
                             '' AS ToDataBase,
                             i.Query, 
                             i.SourceColumnList AS FromColumnList, 
                             i.SourceColumnList AS ToColumnList, 
                             i.ManText AS MappedColumnList, 
                             i.BeforeQuery, 
                             i.AfterQuert AS AfterQuery, 
                             i.IsTruncate, 
                             i.IsDeleteAndInsert AS IsDelete, 
                             i.CreatedDate, 
                             1 AS IsActive
                      FROM ImportData i
                      JOIN DataSource d ON d.DataSourceId = i.FromConnectionId 
                      JOIN DataSource s ON s.DataSourceId = i.ToConnectionId 
                      WHERE i.Id = @ImportId", connection))
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
                            FromDataBase = reader.IsDBNull(reader.GetOrdinal("FromDataBase")) ? string.Empty : reader.GetString(reader.GetOrdinal("FromDataBase")),
                            ToDataBase = reader.IsDBNull(reader.GetOrdinal("ToDataBase")) ? string.Empty : reader.GetString(reader.GetOrdinal("ToDataBase")),
                            Query = reader.IsDBNull(reader.GetOrdinal("Query")) ? string.Empty : reader.GetString(reader.GetOrdinal("Query")),
                            FromColumnList = reader.GetString(reader.GetOrdinal("FromColumnList")),
                            ToColumnList = reader.GetString(reader.GetOrdinal("ToColumnList")),
                            MappedColumnList = reader.IsDBNull(reader.GetOrdinal("MappedColumnList")) ? string.Empty : reader.GetString(reader.GetOrdinal("MappedColumnList")),
                            BeforeQuery = reader.IsDBNull(reader.GetOrdinal("BeforeQuery")) ? string.Empty : reader.GetString(reader.GetOrdinal("BeforeQuery")),
                            AfterQuery = reader.IsDBNull(reader.GetOrdinal("AfterQuery")) ? string.Empty : reader.GetString(reader.GetOrdinal("AfterQuery")),
                            IsTruncate = ReadBooleanSafe(reader, "IsTruncate"),
                            IsDelete = ReadBooleanSafe(reader, "IsDelete"),
                            CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                            IsActive = ReadBooleanSafe(reader, "IsActive")
                        };
                    }
                }

                if (importData != null)
                {
                    // Second query to get the source data source
                    using (var command = new SqlCommand(
                        @"SELECT DataSourceId, DatasourceName, ServerName, UserName, Password, 
                          Authentication AS AuthenticationType, DefaultDatabaseName, UserId, CreatedDate,1 AS IsActive
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
                                IsActive = ReadBooleanSafe(reader, "IsActive")
                            };
                        }
                    }

                    // Third query to get the destination data source
                    using (var command = new SqlCommand(
                        @"SELECT DataSourceId, DatasourceName, ServerName, UserName, Password, 
                          Authentication AS AuthenticationType, DefaultDatabaseName, UserId, CreatedDate,1 AS IsActive
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
                                IsActive = ReadBooleanSafe(reader, "IsActive")
                            };
                        }
                    }
                }
            }

            return importData;
        }
    }
} 