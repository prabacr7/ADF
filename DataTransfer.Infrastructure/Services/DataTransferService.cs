using DataTransfer.Core.Entities;
using DataTransfer.Core.Enums;
using DataTransfer.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using Dapper;

namespace DataTransfer.Infrastructure.Services
{
    public class DataTransferService : IDataTransferService
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<DataTransferService> _logger;

        public DataTransferService(
            IDatabaseService databaseService,
            ILogger<DataTransferService> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

        public async Task<IEnumerable<string>> GetSourceTablesAsync(DatabaseConnection connection)
        {
            return await _databaseService.GetTablesAsync(connection);
        }

        public async Task<IEnumerable<string>> GetDestinationTablesAsync(DatabaseConnection connection)
        {
            return await _databaseService.GetTablesAsync(connection);
        }

        public async Task<TableInfo> GetSourceTableInfoAsync(DatabaseConnection connection, string tableName)
        {
            return await _databaseService.GetTableInfoAsync(connection, tableName);
        }

        public async Task<TableInfo> GetDestinationTableInfoAsync(DatabaseConnection connection, string tableName)
        {
            return await _databaseService.GetTableInfoAsync(connection, tableName);
        }

        public async Task<TransferResult> TransferDataAsync(TransferRequest request, IProgress<int>? progress = null)
        {
            var result = new TransferResult();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Get table info for source and destination
                var sourceTableInfo = await GetSourceTableInfoAsync(request.SourceConnection, request.SourceTable);
                var destTableInfo = await GetDestinationTableInfoAsync(request.DestinationConnection, request.DestinationTable);
                
                // Execute before script if provided
                if (!string.IsNullOrEmpty(request.BeforeScript))
                {
                    await _databaseService.ExecuteQueryAsync(request.DestinationConnection, request.BeforeScript);
                    result.Messages.Add("Executed before script successfully");
                }

                // Clear destination table based on transfer mode
                if (request.TransferMode == TransferMode.TruncateAndInsert)
                {
                    var truncateSql = $"TRUNCATE TABLE {destTableInfo.FullName}";
                    await _databaseService.ExecuteQueryAsync(request.DestinationConnection, truncateSql);
                    result.Messages.Add($"Truncated table {destTableInfo.FullName}");
                }
                else if (request.TransferMode == TransferMode.DeleteAndInsert)
                {
                    var deleteSql = $"DELETE FROM {destTableInfo.FullName}";
                    await _databaseService.ExecuteQueryAsync(request.DestinationConnection, deleteSql);
                    result.Messages.Add($"Deleted all rows from {destTableInfo.FullName}");
                }

                // Create column lists for query
                var includeColumns = request.ColumnMappings.Where(m => m.IsIncluded).ToList();
                
                var sourceColumns = string.Join(", ", includeColumns.Select(m => $"[{m.SourceColumn}]"));
                var destColumns = string.Join(", ", includeColumns.Select(m => $"[{m.DestinationColumn}]"));
                
                // Get total count for progress reporting
                var countSql = $"SELECT COUNT(*) FROM {sourceTableInfo.FullName}";
                var totalRows = await _databaseService.QueryAsync(request.SourceConnection, countSql);
                var total = (long)(totalRows.First().Count);
                
                // Set up batch processing
                long processed = 0;
                var batchSize = request.BatchSize;

                using (var sourceConn = new SqlConnection(request.SourceConnection.ConnectionString))
                using (var destConn = new SqlConnection(request.DestinationConnection.ConnectionString))
                {
                    await sourceConn.OpenAsync();
                    await destConn.OpenAsync();

                    // Read data in batches and insert
                    var offset = 0;
                    bool hasMoreData = true;

                    while (hasMoreData)
                    {
                        var batchSql = $@"
                            SELECT {sourceColumns} 
                            FROM {sourceTableInfo.FullName} 
                            ORDER BY (SELECT NULL)
                            OFFSET {offset} ROWS
                            FETCH NEXT {batchSize} ROWS ONLY";
                        
                        var data = await sourceConn.QueryAsync(batchSql);
                        var rowCount = data.Count();
                        
                        if (rowCount == 0)
                        {
                            hasMoreData = false;
                            continue;
                        }

                        // Build bulk insert
                        using (var bulkCopy = new SqlBulkCopy(destConn))
                        {
                            bulkCopy.DestinationTableName = destTableInfo.FullName;
                            bulkCopy.BatchSize = batchSize;
                            
                            // Map columns
                            for (int i = 0; i < includeColumns.Count; i++)
                            {
                                bulkCopy.ColumnMappings.Add(includeColumns[i].SourceColumn, includeColumns[i].DestinationColumn);
                            }

                            // Create DataTable from result
                            var dataTable = new DataTable();
                            var firstRow = data.First() as IDictionary<string, object>;
                            
                            foreach (var col in includeColumns)
                            {
                                dataTable.Columns.Add(col.SourceColumn);
                            }

                            foreach (var row in data)
                            {
                                var dataRow = dataTable.NewRow();
                                var rowDict = row as IDictionary<string, object>;
                                
                                foreach (var col in includeColumns)
                                {
                                    dataRow[col.SourceColumn] = rowDict[col.SourceColumn];
                                }
                                
                                dataTable.Rows.Add(dataRow);
                            }

                            // Write to destination
                            await bulkCopy.WriteToServerAsync(dataTable);
                        }

                        processed += rowCount;
                        offset += batchSize;
                        
                        // Report progress
                        if (progress != null)
                        {
                            var percentComplete = (int)((double)processed / total * 100);
                            progress.Report(percentComplete);
                        }
                        
                        _logger.LogInformation("Transferred {Processed} of {Total} rows", processed, total);
                    }
                }

                // Execute after script if provided
                if (!string.IsNullOrEmpty(request.AfterScript))
                {
                    await _databaseService.ExecuteQueryAsync(request.DestinationConnection, request.AfterScript);
                    result.Messages.Add("Executed after script successfully");
                }

                result.IsSuccess = true;
                result.RowsTransferred = processed;
                
                _logger.LogInformation("Successfully transferred {Rows} rows", processed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data transfer");
                result.IsSuccess = false;
                result.Error = ex;
                result.Messages.Add($"Error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.Messages.Add($"Transfer completed in {result.Duration.TotalSeconds:N2} seconds");
            }

            return result;
        }
    }
} 