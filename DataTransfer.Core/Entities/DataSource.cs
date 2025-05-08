using System;

namespace DataTransfer.Core.Entities
{
    public class DataSource
    {
        public int DataSourceId { get; set; }
        public string DatasourceName { get; set; }
        public string ServerName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string AuthenticationType { get; set; }
        public string DefaultDatabaseName { get; set; }
        public int? UserId { get; set; }
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }
} 