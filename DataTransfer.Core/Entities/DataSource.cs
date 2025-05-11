using System;

namespace DataTransfer.Core.Entities
{
    public class DataSource
    {
        public int DataSourceId { get; set; }
        public string DatasourceName { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string AuthenticationType { get; set; } = string.Empty;
        public string DefaultDatabaseName { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }
} 