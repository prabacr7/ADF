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
        private readonly IConnectionStringManager _connectionManager;
        private readonly int _defaultBatchSize = 1000;
        private readonly int _defaultCommandTimeout = 6000; // 100 minutes

        public DataTransferExecutor(
            ILogger<DataTransferExecutor> logger, 
            ForeignKeyHelper foreignKeyHelper,
            IConnectionStringManager connectionManager)
        {
            _logger = logger;
            _foreignKeyHelper = foreignKeyHelper;
            _connectionManager = connectionManager;
        }

        public async Task<bool> ExecuteImportAsync(ImportData importData, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting import job: {JobName} (ID: {ImportId})", importData.Name, importData.ImportId);
            
            SqlConnection sourceConnection = null;
            SqlConnection destinationConnection = null;
            SqlConnection managementConnection = null;
            
            try
            {
                // Create connections using the connection manager
                sourceConnection = await _connectionManager.CreateSourceConnectionAsync(importData.ImportId, cancellationToken);
                destinationConnection = await _connectionManager.CreateDestinationConnectionAsync(importData.ImportId, cancellationToken);
                managementConnection = await _connectionManager.CreateDestinationConnectionAsync(importData.ImportId, cancellationToken);
                
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
            finally
            {
                // Dispose of connections
                sourceConnection?.Dispose();
                destinationConnection?.Dispose();
                managementConnection?.Dispose();
            }
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
            try
            {
                // Validate input to avoid SQL injection
                if (string.IsNullOrEmpty(importData.FromColumnList) || string.IsNullOrEmpty(importData.ToColumnList))
                {
                    _logger.LogError("Missing required column lists for import {ImportId}", importData.ImportId);
                    throw new ArgumentException("Source or destination column lists cannot be empty");
                }

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

                // Validate we have column information
                if (sourceColumns.Length == 0 || destColumns.Length == 0)
                {
                    _logger.LogError("Empty column list after parsing for import {ImportId}", importData.ImportId);
                    throw new ArgumentException("No valid columns found after parsing source or destination lists");
                }

                _logger.LogDebug("Preparing query with {SourceColumns} source columns, {DestColumns} destination columns, and {MappedColumns} mapped values",
                    sourceColumns.Length, destColumns.Length, mappedColumns.Length);

                // Build SELECT list
                List<string> selectItems = new List<string>();
                for (int i = 0; i < Math.Min(sourceColumns.Length, destColumns.Length); i++)
                {
                    if (i < mappedColumns.Length && !string.IsNullOrEmpty(mappedColumns[i]))
                    {
                        // Using mapped value (constant) - escape single quotes in the constant
                        string escapedValue = mappedColumns[i].Replace("'", "''");
                        selectItems.Add($"'{escapedValue}' AS [{destColumns[i]}]");
                    }
                    else if (!string.IsNullOrEmpty(sourceColumns[i]))
                    {
                        // Using source column - ensure proper bracket wrapping for name with special characters
                        selectItems.Add($"[{sourceColumns[i].Replace("]", "]]")}]");
                    }
                }

                string selectList = string.Join(", ", selectItems);
                
                // Verify we generated a non-empty select list
                if (string.IsNullOrEmpty(selectList))
                {
                    _logger.LogError("Generated empty SELECT list for import {ImportId}", importData.ImportId);
                    throw new InvalidOperationException("Unable to generate SELECT statement - no valid columns");
                }

                // Build final query
                string finalQuery;
                if (!string.IsNullOrEmpty(importData.Query))
                {
                    finalQuery = $"SELECT {selectList} FROM ({importData.Query}) AS QueryResult";
                }
                else
                {
                    // Ensure table name is properly escaped
                    string tableReference = importData.FromTable;
                    if (!tableReference.Contains("[") && !tableReference.Contains("]"))
                    {
                        // Simple table name, wrap in brackets
                        tableReference = $"[{tableReference}]";
                    }
                    finalQuery = $"SELECT {selectList} FROM {tableReference}";
                }

                _logger.LogDebug("Generated source query: {Query}", finalQuery);
                return finalQuery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing source query for import {ImportId}", importData.ImportId);
                throw;
            }
        }

        private List<SqlBulkCopyColumnMapping> GetColumnMappings(ImportData importData)
        {
            var columnMappings = new List<SqlBulkCopyColumnMapping>();
            
            try
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
                
                _logger.LogDebug("Creating column mappings. Source columns: {SourceCount}, Dest columns: {DestCount}, Mapped values: {MappedCount}", 
                    sourceColumns.Length, destColumns.Length, mappedColumns.Length);
                
                // Validate we have column information
                if (sourceColumns.Length == 0 || destColumns.Length == 0)
                {
                    _logger.LogError("Empty column lists for import {ImportId}", importData.ImportId);
                    throw new ArgumentException("No columns to map - source or destination column lists are empty");
                }
                
                // Create mappings
                int sourceOrdinal = 0; // Track the ordinal position in the result set
                
                for (int i = 0; i < Math.Min(sourceColumns.Length, destColumns.Length); i++)
                {
                    bool isSourceMapped = i < mappedColumns.Length && !string.IsNullOrEmpty(mappedColumns[i]);
                    
                    if (isSourceMapped)
                    {
                        // This is a mapped constant value (from ManText/MappedColumnList)
                        // The ordinal will be the position in our result set
                        _logger.LogDebug("Adding mapping for constant value -> {DestColumn} at ordinal {Ordinal}", 
                            destColumns[i], sourceOrdinal);
                        
                        columnMappings.Add(new SqlBulkCopyColumnMapping(sourceOrdinal, destColumns[i]));
                        sourceOrdinal++; // Increment for the next source column
                    }
                    else if (!string.IsNullOrEmpty(sourceColumns[i]) && !string.IsNullOrEmpty(destColumns[i]))
                    {
                        // Direct column mapping by name or ordinal
                        _logger.LogDebug("Adding mapping for source column {SourceColumn} -> {DestColumn} at ordinal {Ordinal}", 
                            sourceColumns[i], destColumns[i], sourceOrdinal);
                        
                        // Try to use ordinal mapping which is more reliable
                        columnMappings.Add(new SqlBulkCopyColumnMapping(sourceOrdinal, destColumns[i]));
                        sourceOrdinal++; // Increment for the next source column
                    }
                }
                
                if (columnMappings.Count == 0)
                {
                    _logger.LogError("No valid column mappings could be created for import {ImportId}", importData.ImportId);
                    throw new InvalidOperationException("Failed to create any valid column mappings");
                }

                _logger.LogDebug("Created {Count} column mappings", columnMappings.Count);
                return columnMappings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating column mappings for import {ImportId}", importData.ImportId);
                throw;
            }
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
                _logger.LogDebug("Opening source connection to {Database}", sourceConnection.Database);
                await sourceConnection.OpenAsync(cancellationToken);
            }
            
            if (destinationConnection.State != ConnectionState.Open)
            {
                _logger.LogDebug("Opening destination connection to {Database}", destinationConnection.Database);
                await destinationConnection.OpenAsync(cancellationToken);
            }

            // Log the query for diagnostics
            _logger.LogDebug("Executing source query: {Query}", sourceQuery);
            
            using var command = new SqlCommand(sourceQuery, sourceConnection);
            command.CommandTimeout = _defaultCommandTimeout;

            try
            {
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                _logger.LogDebug("SqlDataReader initialized successfully. HasRows: {HasRows}", reader.HasRows);

                // If reader has no rows, log it and return early
                if (!reader.HasRows)
                {
                    _logger.LogWarning("Query returned no rows: {Query}", sourceQuery);
                    return 0;
                }

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
                    
                    _logger.LogDebug("Schema table contains {ColumnCount} columns", schemaTable.Rows.Count);
                }
                else
                {
                    _logger.LogWarning("Failed to get schema table from reader");
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
                    _logger.LogDebug("Added column mapping: {SourceColumn} -> {DestinationColumn}", 
                        mapping.SourceColumn, mapping.DestinationColumn);
                }

                // Process the data in batches
                int batchCount = 0;
                bool readSuccess = false;
                
                try
                {
                    _logger.LogDebug("Starting to read rows from source");
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        readSuccess = true;
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
                    
                    if (!readSuccess)
                    {
                        _logger.LogWarning("reader.ReadAsync() never returned true - possibly empty result set or reader issue");
                    }

                    // Process any remaining rows
                    if (dataTable.Rows.Count > 0)
                    {
                        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                        totalRowsMoved += dataTable.Rows.Count;
                        _logger.LogDebug("Copied final {BatchSize} rows, total: {TotalRows}", dataTable.Rows.Count, totalRowsMoved);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading from SqlDataReader or writing to bulk copy");
                    throw;
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL error executing query. Error number: {ErrorNumber}, State: {State}, Message: {Message}", 
                    sqlEx.Number, sqlEx.State, sqlEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing bulk copy operation");
                throw;
            }

            _logger.LogInformation("Bulk copy operation completed. Moved {RowCount} rows to {DestinationTable}", 
                totalRowsMoved, destinationTable);
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