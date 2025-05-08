using DataTransfer.Application.Commands;
using DataTransfer.Application.DTOs;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransferController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IValidator<TransferRequestDto> _validator;
        private readonly ILogger<TransferController> _logger;

        public TransferController(
            IMediator mediator,
            IValidator<TransferRequestDto> validator,
            ILogger<TransferController> logger)
        {
            _mediator = mediator;
            _validator = validator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ExecuteTransfer([FromBody] TransferRequestDto request)
        {
            try
            {
                // Validate the request
                var validationResult = await _validator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.Errors);
                }

                var command = new ExecuteTransferCommand
                {
                    TransferRequest = request
                };

                var result = await _mediator.Send(command);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing transfer");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
} 