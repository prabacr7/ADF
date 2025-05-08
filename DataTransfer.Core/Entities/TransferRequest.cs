using DataTransfer.Core.Enums;

namespace DataTransfer.Core.Entities
{
    public class TransferRequest
    {
        public DatabaseConnection SourceConnection { get; set; } = new DatabaseConnection();
        public DatabaseConnection DestinationConnection { get; set; } = new DatabaseConnection();
        public string SourceTable { get; set; } = string.Empty;
        public string DestinationTable { get; set; } = string.Empty;
        public List<ColumnMapping> ColumnMappings { get; set; } = new List<ColumnMapping>();
        public TransferMode TransferMode { get; set; } = TransferMode.TruncateAndInsert;
        public string? BeforeScript { get; set; }
        public string? AfterScript { get; set; }
        public int BatchSize { get; set; } = 1000;
    }

    public class ColumnMapping
    {
        public string SourceColumn { get; set; } = string.Empty;
        public string DestinationColumn { get; set; } = string.Empty;
        public bool IsIncluded { get; set; } = true;
    }
} 