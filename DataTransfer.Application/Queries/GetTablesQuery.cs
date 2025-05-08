using DataTransfer.Application.DTOs;
using MediatR;

namespace DataTransfer.Application.Queries
{
    public class GetTablesQuery : IRequest<IEnumerable<string>>
    {
        public DatabaseConnectionDto Connection { get; set; } = new DatabaseConnectionDto();
        public bool IsSource { get; set; }
    }
} 