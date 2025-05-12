using System;

namespace DataTransfer.API.Models
{
    public class ImportDataDto
    {
        public int? UserId { get; set; }
        public int? FromConnectionId { get; set; }
        public int? ToConnectionId { get; set; }
        public string FromDataBase { get; set; }
        public string ToDataBase { get; set; }
        public string FromTableName { get; set; }
        public string ToTableName { get; set; }
        public string Query { get; set; }
        public string SourceColumnList { get; set; }
        public string DescColumnList { get; set; }
        public string ManText { get; set; }
        public string Description { get; set; }
        public string IsTruncate { get; set; }
        public string IsDeleteAndInsert { get; set; }
        public string BeforeQuery { get; set; }
        public string AfterQuery { get; set; }
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
        public string CronJob { get; set; }
        
        // Not sent by client, calculated on server
        public DateTime? NextRunDateTime { get; internal set; }
        public DateTime? LastRunDateTime { get; internal set; }
    }

    public class ImportDataResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? ImportId { get; set; }
    }
} 