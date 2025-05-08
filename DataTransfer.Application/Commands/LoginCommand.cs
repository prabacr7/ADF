using System.Threading;
using System.Threading.Tasks;
using DataTransfer.Application.DTOs;
using DataTransfer.Application.Services;
using DataTransfer.Core.Interfaces;
using MediatR;

namespace DataTransfer.Application.Commands
{
    public class LoginCommand : IRequest<AuthResponseDto>
    {
        public LoginRequestDto LoginRequest { get; set; } = new LoginRequestDto();
    }
    
    public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly AuthService _authService;
        
        public LoginCommandHandler(IUserRepository userRepository, AuthService authService)
        {
            _userRepository = userRepository;
            _authService = authService;
        }
        
        public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.AuthenticateUserAsync(
                request.LoginRequest.UserName, 
                request.LoginRequest.Password);
                
            if (user == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid username or password"
                };
            }
            
            var token = _authService.GenerateJwtToken(user);
            
            return new AuthResponseDto
            {
                Success = true,
                Token = token,
                UserName = user.UserName,
                Message = "Login successful",
                UserId = user.UserId
            };
        }
    }
} 