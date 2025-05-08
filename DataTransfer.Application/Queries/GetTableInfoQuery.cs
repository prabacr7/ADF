using DataTransfer.Application.DTOs;
using MediatR;

namespace DataTransfer.Application.Queries
{
    public class GetTableInfoQuery : IRequest<TableInfoDto>
    {
        public DatabaseConnectionDto Connection { get; set; } = new DatabaseConnectionDto();
        public string TableName { get; set; } = string.Empty;
        public bool IsSource { get; set; }
    }
} 