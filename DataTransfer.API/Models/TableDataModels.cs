using System.ComponentModel.DataAnnotations;

namespace DataTransfer.API.Models
{
    public class ConnectionRequestDto
    {
        [Required]
        public string ServerName { get; set; } = string.Empty;
        
        [Required]
        public string Database { get; set; } = string.Empty;
        
        [Required]
        public string Authentication { get; set; } = string.Empty;
        
        public string? UserName { get; set; }
        
        public string? Password { get; set; }
        
        public int? UserId { get; set; }
        
        public bool TrustServerCertificate { get; set; } = true;
    }

    public class QueryRequestDto : ConnectionRequestDto
    {
        [Required]
        public string Query { get; set; } = string.Empty;
        
        public int? MaxRows { get; set; } = 1000;
    }

    public class TableDataRequestDto : ConnectionRequestDto
    {
        [Required]
        public string TableName { get; set; } = string.Empty;
        
        public int PageIndex { get; set; }
        
        public int PageSize { get; set; }
    }

    public class QueryDataRequestDto : ConnectionRequestDto
    {
        [Required]
        public string Query { get; set; } = string.Empty;
        
        public int PageIndex { get; set; }
        
        public int PageSize { get; set; }
    }

    public class QueryResultDto<T>
    {
        [Required]
        public List<T> Data { get; set; } = new List<T>();
        
        public int TotalCount { get; set; }
        
        public int PageIndex { get; set; }
        
        public int PageSize { get; set; }
    }

    public class GridDataRequestDto : ConnectionRequestDto
    {
        [Required]
        public string TableName { get; set; } = string.Empty;
        
        public int StartRow { get; set; }
        
        public int EndRow { get; set; }
        
        public List<SortModelItem>? SortModel { get; set; }
        
        public Dictionary<string, FilterModelItem>? FilterModel { get; set; }
    }

    public class GridQueryRequestDto : ConnectionRequestDto
    {
        [Required]
        public string Query { get; set; } = string.Empty;
        
        public int StartRow { get; set; }
        
        public int EndRow { get; set; }
        
        public List<SortModelItem>? SortModel { get; set; }
        
        public Dictionary<string, FilterModelItem>? FilterModel { get; set; }
    }

    public class SortModelItem
    {
        [Required]
        public string ColId { get; set; } = string.Empty;
        
        [Required]
        public string Sort { get; set; } = string.Empty;
    }

    public class FilterModelItem
    {
        [Required]
        public string Type { get; set; } = string.Empty;
        
        [Required]
        public string Filter { get; set; } = string.Empty;
        
        [Required]
        public string Operator { get; set; } = string.Empty;
        
        [Required]
        public string Condition1 { get; set; } = string.Empty;
        
        public string? Condition2 { get; set; }
    }

    public class GridDataResultDto<T>
    {
        [Required]
        public List<T> Data { get; set; } = new List<T>();
        
        public int TotalCount { get; set; }
        
        public int StartRow { get; set; }
        
        public int EndRow { get; set; }
    }

    public class DatabaseFilterDto : ConnectionRequestDto
    {
        public bool? ExcludeSystemDatabases { get; set; } = true;
        public string? NameFilter { get; set; }
        public bool? OnlyOnlineDatabases { get; set; } = true;
        public int? MinimumCompatibilityLevel { get; set; }
    }
} 