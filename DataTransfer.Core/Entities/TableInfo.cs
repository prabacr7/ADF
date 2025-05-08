namespace DataTransfer.Core.Entities
{
    public class TableInfo
    {
        public string Schema { get; set; } = "dbo";
        public string Name { get; set; } = string.Empty;
        public string FullName => $"[{Schema}].[{Name}]";
        public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public int OrdinalPosition { get; set; }
    }
} 