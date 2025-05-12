using System;
using System.Collections.Generic;

namespace DataTransfer.Core.Entities
{
    public class ImportData
    {
        public int ImportId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FromDataSourceId { get; set; }
        public int ToDataSourceId { get; set; }
        public string FromTable { get; set; } = string.Empty;
        public string ToTable { get; set; } = string.Empty;
        public string FromDataBase { get; set; } = string.Empty;
        public string ToDataBase { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string FromColumnList { get; set; } = string.Empty;
        public string ToColumnList { get; set; } = string.Empty;
        public string MappedColumnList { get; set; } = string.Empty;
        public string BeforeQuery { get; set; } = string.Empty;
        public string AfterQuery { get; set; } = string.Empty;
        public bool IsTruncate { get; set; }
        public bool IsDelete { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string CronJob { get; set; } = string.Empty;
        
        // Navigation properties
        public DataSource? FromDataSource { get; set; }
        public DataSource? ToDataSource { get; set; }
    }
} 