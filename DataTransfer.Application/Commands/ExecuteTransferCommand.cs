using DataTransfer.Application.DTOs;
using MediatR;

namespace DataTransfer.Application.Commands
{
    public class ExecuteTransferCommand : IRequest<TransferResultDto>
    {
        public TransferRequestDto TransferRequest { get; set; } = new TransferRequestDto();
    }
} 