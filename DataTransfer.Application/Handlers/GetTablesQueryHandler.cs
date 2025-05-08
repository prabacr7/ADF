using DataTransfer.Application.DTOs;
using DataTransfer.Application.Queries;
using DataTransfer.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataTransfer.Application.Handlers
{
    public class GetTablesQueryHandler : IRequestHandler<GetTablesQuery, IEnumerable<string>>
    {
        private readonly IDataTransferService _dataTransferService;
        private readonly ILogger<GetTablesQueryHandler> _logger;

        public GetTablesQueryHandler(
            IDataTransferService dataTransferService,
            ILogger<GetTablesQueryHandler> logger)
        {
            _dataTransferService = dataTransferService;
            _logger = logger;
        }

        public async Task<IEnumerable<string>> Handle(GetTablesQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var connection = DatabaseConnectionDto.ToEntity(request.Connection);
                
                if (request.IsSource)
                {
                    return await _dataTransferService.GetSourceTablesAsync(connection);
                }
                else
                {
                    return await _dataTransferService.GetDestinationTablesAsync(connection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables");
                throw;
            }
        }
    }
} 