using DataTransfer.Application.Commands;
using DataTransfer.Application.DTOs;
using DataTransfer.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataTransfer.Application.Handlers
{
    public class ExecuteTransferCommandHandler : IRequestHandler<ExecuteTransferCommand, TransferResultDto>
    {
        private readonly IDataTransferService _dataTransferService;
        private readonly ILogger<ExecuteTransferCommandHandler> _logger;

        public ExecuteTransferCommandHandler(
            IDataTransferService dataTransferService,
            ILogger<ExecuteTransferCommandHandler> logger)
        {
            _dataTransferService = dataTransferService;
            _logger = logger;
        }

        public async Task<TransferResultDto> Handle(ExecuteTransferCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting data transfer operation");
                
                var transferRequest = TransferRequestDto.ToEntity(request.TransferRequest);
                var progress = new Progress<int>(percent =>
                {
                    _logger.LogInformation("Transfer progress: {Percent}%", percent);
                });

                var result = await _dataTransferService.TransferDataAsync(transferRequest, progress);
                return TransferResultDto.FromEntity(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data transfer execution");
                throw;
            }
        }
    }
} 