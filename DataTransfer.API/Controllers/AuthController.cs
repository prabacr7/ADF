using DataTransfer.Application.Commands;
using DataTransfer.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IMediator mediator, ILogger<AuthController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var command = new LoginCommand
                {
                    LoginRequest = request
                };

                var result = await _mediator.Send(command);
                
                if (!result.Success)
                {
                    return Unauthorized(result);
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { error = "An error occurred during login" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto request)
        {
            try
            {
                var command = new RegisterCommand
                {
                    RegisterRequest = request
                };

                var result = await _mediator.Send(command);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, new { error = "An error occurred during registration" });
            }
        }
    }
} 