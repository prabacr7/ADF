using DataTransfer.Core.Entities;

namespace DataTransfer.Application.DTOs
{
    public class DatabaseConnectionDto
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;

        public static DatabaseConnection ToEntity(DatabaseConnectionDto dto)
        {
            return new DatabaseConnection
            {
                ServerName = dto.ServerName,
                DatabaseName = dto.DatabaseName,
                ConnectionString = dto.ConnectionString
            };
        }

        public static DatabaseConnectionDto FromEntity(DatabaseConnection entity)
        {
            return new DatabaseConnectionDto
            {
                ServerName = entity.ServerName,
                DatabaseName = entity.DatabaseName,
                ConnectionString = entity.ConnectionString
            };
        }
    }
} 