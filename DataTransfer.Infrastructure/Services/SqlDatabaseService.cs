using Dapper;
using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace DataTransfer.Infrastructure.Services
{
    public class SqlDatabaseService : IDatabaseService
    {
        private readonly ILogger<SqlDatabaseService> _logger;

        public SqlDatabaseService(ILogger<SqlDatabaseService> logger)
        {
            _logger = logger;
        }

        public async Task<int> ExecuteQueryAsync(DatabaseConnection connection, string query)
        {
            try
            {
                using (var dbConnection = new SqlConnection(connection.ConnectionString))
                {
                    await dbConnection.OpenAsync();
                    return await dbConnection.ExecuteAsync(query);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Query}", query);
                throw;
            }
        }

        public async Task<TableInfo> GetTableInfoAsync(DatabaseConnection connection, string tableName)
        {
            try
            {
                using (var dbConnection = new SqlConnection(connection.ConnectionString))
                {
                    await dbConnection.OpenAsync();
                    
                    // Split tableName into schema and table if it contains a .
                    string schema = "dbo";
                    string table = tableName;
                    
                    if (tableName.Contains("."))
                    {
                        var parts = tableName.Split('.');
                        schema = parts[0].Trim('[', ']');
                        table = parts[1].Trim('[', ']');
                    }

                    // Get table columns
                    var query = @"
                        SELECT 
                            c.COLUMN_NAME AS Name,
                            c.DATA_TYPE AS DataType,
                            CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                            CASE WHEN kcu.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                            c.ORDINAL_POSITION AS OrdinalPosition
                        FROM 
                            INFORMATION_SCHEMA.COLUMNS c
                        LEFT JOIN 
                            INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON 
                            c.TABLE_SCHEMA = kcu.TABLE_SCHEMA AND
                            c.TABLE_NAME = kcu.TABLE_NAME AND
                            c.COLUMN_NAME = kcu.COLUMN_NAME AND
                            kcu.CONSTRAINT_NAME = (
                                SELECT tc.CONSTRAINT_NAME
                                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                WHERE tc.TABLE_SCHEMA = c.TABLE_SCHEMA
                                AND tc.TABLE_NAME = c.TABLE_NAME
                                AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                            )
                        WHERE 
                            c.TABLE_SCHEMA = @Schema AND c.TABLE_NAME = @Table
                        ORDER BY 
                            c.ORDINAL_POSITION";

                    var columns = await dbConnection.QueryAsync<ColumnInfo>(query, new { Schema = schema, Table = table });

                    return new TableInfo
                    {
                        Schema = schema,
                        Name = table,
                        Columns = columns.ToList()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table info for {Table}", tableName);
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetTablesAsync(DatabaseConnection connection)
        {
            try
            {
                using (var dbConnection = new SqlConnection(connection.ConnectionString))
                {
                    await dbConnection.OpenAsync();
                    
                    var query = @"
                        SELECT SCHEMA_NAME(schema_id) + '.' + name AS TableName
                        FROM sys.tables
                        ORDER BY TableName";

                    return await dbConnection.QueryAsync<string>(query);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables from database");
                throw;
            }
        }

        public async Task<IEnumerable<dynamic>> QueryAsync(DatabaseConnection connection, string query)
        {
            try
            {
                using (var dbConnection = new SqlConnection(connection.ConnectionString))
                {
                    await dbConnection.OpenAsync();
                    return await dbConnection.QueryAsync(query);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Query}", query);
                throw;
            }
        }
    }
} 