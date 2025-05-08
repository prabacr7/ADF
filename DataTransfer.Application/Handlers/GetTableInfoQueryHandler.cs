using DataTransfer.Application.DTOs;
using DataTransfer.Application.Queries;
using DataTransfer.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataTransfer.Application.Handlers
{
    public class GetTableInfoQueryHandler : IRequestHandler<GetTableInfoQuery, TableInfoDto>
    {
        private readonly IDataTransferService _dataTransferService;
        private readonly ILogger<GetTableInfoQueryHandler> _logger;

        public GetTableInfoQueryHandler(
            IDataTransferService dataTransferService,
            ILogger<GetTableInfoQueryHandler> logger)
        {
            _dataTransferService = dataTransferService;
            _logger = logger;
        }

        public async Task<TableInfoDto> Handle(GetTableInfoQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var connection = DatabaseConnectionDto.ToEntity(request.Connection);
                
                if (request.IsSource)
                {
                    var tableInfo = await _dataTransferService.GetSourceTableInfoAsync(connection, request.TableName);
                    return TableInfoDto.FromEntity(tableInfo);
                }
                else
                {
                    var tableInfo = await _dataTransferService.GetDestinationTableInfoAsync(connection, request.TableName);
                    return TableInfoDto.FromEntity(tableInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table info for {TableName}", request.TableName);
                throw;
            }
        }
    }
} 