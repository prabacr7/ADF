namespace DataTransfer.Core.Entities
{
    public class DatabaseConnection
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }
} 