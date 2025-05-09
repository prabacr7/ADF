using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Services
{
    public class ForeignKeyHelper
    {
        private readonly ILogger<ForeignKeyHelper> _logger;

        public ForeignKeyHelper(ILogger<ForeignKeyHelper> logger)
        {
            _logger = logger;
        }

        public async Task<bool> DisableForeignKeysAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken = default)
        {
            try
            {
                string forginKeyQuery = @"
                    DECLARE @TableName NVARCHAR(128) = @Table;
                    DECLARE @SQL NVARCHAR(MAX) = '';
                    SELECT @SQL = STUFF((
                        SELECT CHAR(13) + 'ALTER TABLE [' + SCHEMA_NAME(pt.schema_id) + '].[' + pt.name + '] NOCHECK CONSTRAINT [' + fk.name + '];'
                        FROM sys.foreign_keys fk
                        JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
                        JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
                        WHERE '[' + SCHEMA_NAME(pt.schema_id) + '].[' + OBJECT_NAME(pt.object_id) + ']' = @TableName
                           OR '[' + SCHEMA_NAME(rt.schema_id) + '].[' + OBJECT_NAME(rt.object_id) + ']' = @TableName
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'),
                    1, 1, '');
                    
                    EXEC sp_executesql @SQL;";

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var command = new SqlCommand(forginKeyQuery, connection);
                command.CommandTimeout = 600; // 10 minutes
                command.Parameters.AddWithValue("@Table", tableName);
                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Foreign key constraints disabled for table {TableName}", tableName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling foreign key constraints for table {TableName}", tableName);
                return false;
            }
        }

        public async Task<bool> EnableForeignKeysAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken = default)
        {
            try
            {
                string forginKeyQuery = @"
                    DECLARE @TableName NVARCHAR(128) = @Table;
                    DECLARE @SQL NVARCHAR(MAX) = '';
                    SELECT @SQL = STUFF((
                        SELECT CHAR(13) + 'ALTER TABLE [' + SCHEMA_NAME(pt.schema_id) + '].[' + pt.name + '] WITH CHECK CHECK CONSTRAINT [' + fk.name + '];'
                        FROM sys.foreign_keys fk
                        JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
                        JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
                        WHERE '[' + SCHEMA_NAME(pt.schema_id) + '].[' + OBJECT_NAME(pt.object_id) + ']' = @TableName
                           OR '[' + SCHEMA_NAME(rt.schema_id) + '].[' + OBJECT_NAME(rt.object_id) + ']' = @TableName
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'),
                    1, 1, '');
                    
                    EXEC sp_executesql @SQL;";

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var command = new SqlCommand(forginKeyQuery, connection);
                command.CommandTimeout = 600; // 10 minutes
                command.Parameters.AddWithValue("@Table", tableName);
                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Foreign key constraints enabled for table {TableName}", tableName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling foreign key constraints for table {TableName}", tableName);
                return false;
            }
        }
    }
} 