using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Infrastructure.Services
{
    public class DataTransferExecutor : IImportExecutor
    {
        private readonly ILogger<DataTransferExecutor> _logger;
        private readonly ForeignKeyHelper _foreignKeyHelper;
        private readonly int _defaultBatchSize = 1000;
        private readonly int _defaultCommandTimeout = 6000; // 100 minutes

        public DataTransferExecutor(ILogger<DataTransferExecutor> logger, ForeignKeyHelper foreignKeyHelper)
        {
            _logger = logger;
            _foreignKeyHelper = foreignKeyHelper;
        }

        public async Task<bool> ExecuteImportAsync(ImportData importData, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting import job: {JobName} (ID: {ImportId})", importData.Name, importData.ImportId);
            
            if (importData.FromDataSource == null || importData.ToDataSource == null)
            {
                _logger.LogError("Source or destination data source is missing for import ID: {ImportId}", importData.ImportId);
                return false;
            }

            using var sourceConnection = CreateConnection(importData.FromDataSource);
            using var destinationConnection = CreateConnection(importData.ToDataSource);
            using var managementConnection = CreateConnection(importData.ToDataSource);
            
            bool disabledForeignKeys = false;
            try
            {
                // Step 1: Disable foreign keys if needed
                if (importData.IsTruncate || importData.IsDelete || !string.IsNullOrEmpty(importData.BeforeQuery))
                {
                    disabledForeignKeys = await _foreignKeyHelper.DisableForeignKeysAsync(managementConnection, importData.ToTable, cancellationToken);
                }

                // Step 2: Run before query / truncate / delete
                if (!string.IsNullOrEmpty(importData.BeforeQuery) || importData.IsTruncate || importData.IsDelete)
                {
                    string commandText = "";
                    
                    if (!string.IsNullOrEmpty(importData.BeforeQuery))
                    {
                        commandText += importData.BeforeQuery;
                        if (!commandText.EndsWith(";"))
                            commandText += ";";
                    }
                    
                    if (importData.IsTruncate)
                    {
                        commandText += $" TRUNCATE TABLE {importData.ToTable};";
                        _logger.LogInformation("Truncating table: {Table}", importData.ToTable);
                    }
                    else if (importData.IsDelete)
                    {
                        commandText += $" DELETE FROM {importData.ToTable};";
                        _logger.LogInformation("Deleting all rows from table: {Table}", importData.ToTable);
                    }

                    if (!string.IsNullOrEmpty(commandText))
                    {
                        await ExecuteCommandAsync(managementConnection, commandText, _defaultCommandTimeout, cancellationToken);
                    }
                }

                // Step 3: Prepare column lists and build source query
                string sourceSelect = PrepareSourceQuery(importData);
                
                // Step 4: Execute bulk copy
                int rowsCopied = await ExecuteBulkCopyAsync(
                    sourceConnection, 
                    destinationConnection, 
                    sourceSelect, 
                    importData.ToTable, 
                    GetColumnMappings(importData),
                    cancellationToken);
                
                _logger.LogInformation("Transferred {RowCount} rows to {DestinationTable}", rowsCopied, importData.ToTable);

                // Step 5: Run after query if specified
                if (!string.IsNullOrEmpty(importData.AfterQuery))
                {
                    _logger.LogInformation("Executing after-script for import ID: {ImportId}", importData.ImportId);
                    await ExecuteCommandAsync(managementConnection, importData.AfterQuery, _defaultCommandTimeout, cancellationToken);
                }

                _logger.LogInformation("Import job completed successfully: {JobName} (ID: {ImportId})", importData.Name, importData.ImportId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing import job: {JobName} (ID: {ImportId})", importData.Name, importData.ImportId);
                return false;
            }
            finally
            {
                // Re-enable foreign keys if we disabled them
                if (disabledForeignKeys)
                {
                    await _foreignKeyHelper.EnableForeignKeysAsync(managementConnection, importData.ToTable, cancellationToken);
                }
            }
        }

        private SqlConnection CreateConnection(DataSource dataSource)
        {
            string connectionString;
            
            if (dataSource.AuthenticationType.Equals("Windows Authentication", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = $"Data Source={dataSource.ServerName};Initial Catalog={dataSource.DefaultDatabaseName};Integrated Security=True;TrustServerCertificate=True;";
            }
            else
            {
                connectionString = $"Data Source={dataSource.ServerName};Initial Catalog={dataSource.DefaultDatabaseName};User Id={dataSource.UserName};Password={dataSource.Password};TrustServerCertificate=True;";
            }
            
            return new SqlConnection(connectionString);
        }

        private async Task ExecuteCommandAsync(SqlConnection connection, string commandText, int commandTimeout, CancellationToken cancellationToken)
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = new SqlCommand(commandText, connection);
            command.CommandTimeout = commandTimeout;
            
            // Add retrying for transient errors
            var retryPolicy = Policy
                .Handle<SqlException>(ex => IsTransientError(ex))
                .WaitAndRetryAsync(3, 
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) => {
                        _logger.LogWarning(exception, "Retry {RetryCount} after {RetrySeconds}s due to transient database error", 
                            retryCount, timeSpan.TotalSeconds);
                    });

            await retryPolicy.ExecuteAsync(async () => await command.ExecuteNonQueryAsync(cancellationToken));
        }

        private string PrepareSourceQuery(ImportData importData)
        {
            // Parse column lists
            string[] sourceColumns = importData.FromColumnList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c != "<-Ignore->")
                .ToArray();
            
            string[] destColumns = importData.ToColumnList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToArray();
            
            string[] mappedColumns = string.IsNullOrEmpty(importData.MappedColumnList) 
                ? Array.Empty<string>() 
                : importData.MappedColumnList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();

            // Build SELECT list
            List<string> selectItems = new List<string>();
            for (int i = 0; i < Math.Min(sourceColumns.Length, destColumns.Length); i++)
            {
                if (i < mappedColumns.Length && !string.IsNullOrEmpty(mappedColumns[i]))
                {
                    // Using mapped value (constant)
                    selectItems.Add($"'{mappedColumns[i]}' AS [{destColumns[i]}]");
                }
                else
                {
                    // Using source column
                    selectItems.Add($"[{sourceColumns[i]}]");
                }
            }

            string selectList = string.Join(", ", selectItems);

            // Build final query
            if (!string.IsNullOrEmpty(importData.Query))
            {
                return $"SELECT {selectList} FROM ({importData.Query}) AS QueryResult";
            }
            else
            {
                return $"SELECT {selectList} FROM {importData.FromTable}";
            }
        }

        private List<SqlBulkCopyColumnMapping> GetColumnMappings(ImportData importData)
        {
            var columnMappings = new List<SqlBulkCopyColumnMapping>();
            
            // Parse column lists
            string[] sourceColumns = importData.FromColumnList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c != "<-Ignore->")
                .ToArray();
            
            string[] destColumns = importData.ToColumnList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToArray();
            
            string[] mappedColumns = string.IsNullOrEmpty(importData.MappedColumnList) 
                ? Array.Empty<string>() 
                : importData.MappedColumnList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();
            
            // Create mappings
            for (int i = 0; i < Math.Min(sourceColumns.Length, destColumns.Length); i++)
            {
                if (i < mappedColumns.Length && !string.IsNullOrEmpty(mappedColumns[i]))
                {
                    // This is a mapped constant value, so the ordinal is the index in our result set
                    columnMappings.Add(new SqlBulkCopyColumnMapping(i, destColumns[i]));
                }
                else
                {
                    // Direct column mapping by name
                    columnMappings.Add(new SqlBulkCopyColumnMapping(sourceColumns[i], destColumns[i]));
                }
            }

            return columnMappings;
        }

        private async Task<int> ExecuteBulkCopyAsync(
            SqlConnection sourceConnection, 
            SqlConnection destinationConnection, 
            string sourceQuery, 
            string destinationTable, 
            List<SqlBulkCopyColumnMapping> columnMappings,
            CancellationToken cancellationToken)
        {
            int totalRowsMoved = 0;
            
            if (sourceConnection.State != ConnectionState.Open)
            {
                await sourceConnection.OpenAsync(cancellationToken);
            }
            
            if (destinationConnection.State != ConnectionState.Open)
            {
                await destinationConnection.OpenAsync(cancellationToken);
            }

            using var command = new SqlCommand(sourceQuery, sourceConnection);
            command.CommandTimeout = _defaultCommandTimeout;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Get the result schema
            DataTable schemaTable = reader.GetSchemaTable();
            DataTable dataTable = new DataTable();

            if (schemaTable != null)
            {
                foreach (DataRow row in schemaTable.Rows)
                {
                    string columnName = Convert.ToString(row["ColumnName"]);
                    DataColumn column = new DataColumn(columnName, (Type)row["DataType"]);
                    column.Unique = (bool)row["IsUnique"];
                    column.AllowDBNull = (bool)row["AllowDBNull"];
                    column.AutoIncrement = (bool)row["IsAutoIncrement"];
                    dataTable.Columns.Add(column);
                }
            }

            // Configure bulk copy
            using var bulkCopy = new SqlBulkCopy(destinationConnection);
            bulkCopy.DestinationTableName = destinationTable;
            bulkCopy.BulkCopyTimeout = _defaultCommandTimeout;
            bulkCopy.BatchSize = _defaultBatchSize;
            bulkCopy.EnableStreaming = true;
            
            // Add column mappings
            foreach (var mapping in columnMappings)
            {
                bulkCopy.ColumnMappings.Add(mapping);
            }

            // Process the data in batches
            int batchCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                DataRow dataRow = dataTable.NewRow();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    dataRow[i] = reader.IsDBNull(i) ? DBNull.Value : reader[i];
                }
                dataTable.Rows.Add(dataRow);
                batchCount++;

                // Process in batches
                if (batchCount >= _defaultBatchSize)
                {
                    await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                    totalRowsMoved += batchCount;
                    _logger.LogDebug("Copied {BatchSize} rows, total: {TotalRows}", batchCount, totalRowsMoved);
                    dataTable.Clear();
                    batchCount = 0;
                }
            }

            // Process any remaining rows
            if (dataTable.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                totalRowsMoved += dataTable.Rows.Count;
                _logger.LogDebug("Copied final {BatchSize} rows, total: {TotalRows}", dataTable.Rows.Count, totalRowsMoved);
            }

            return totalRowsMoved;
        }

        private bool IsTransientError(SqlException ex)
        {
            // Common transient error numbers in SQL Server
            int[] transientErrorNumbers = { 4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001 };
            return transientErrorNumbers.Contains(ex.Number);
        }
    }
} 