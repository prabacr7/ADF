using System.Threading;
using System.Threading.Tasks;
using DataTransfer.Application.DTOs;
using DataTransfer.Application.Services;
using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using MediatR;

namespace DataTransfer.Application.Commands
{
    public class RegisterCommand : IRequest<AuthResponseDto>
    {
        public RegisterUserDto RegisterRequest { get; set; } = new RegisterUserDto();
    }
    
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly AuthService _authService;
        
        public RegisterCommandHandler(IUserRepository userRepository, AuthService authService)
        {
            _userRepository = userRepository;
            _authService = authService;
        }
        
        public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var existingUser = await _userRepository.GetUserByUsernameAsync(request.RegisterRequest.UserName);
            
            if (existingUser != null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Username already exists"
                };
            }
            
            var newUser = new UserLogin
            {
                Name = request.RegisterRequest.Name,
                UserName = request.RegisterRequest.UserName,
                Password = request.RegisterRequest.Password,
                EmailAddress = request.RegisterRequest.EmailAddress
            };
            
            var success = await _userRepository.CreateUserAsync(newUser);
            
            if (!success)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Error creating user"
                };
            }
            
            var token = _authService.GenerateJwtToken(newUser);
            
            return new AuthResponseDto
            {
                Success = true,
                Token = token,
                UserName = newUser.UserName,
                Message = "Registration successful"
            };
        }
    }
} 