using DataTransfer.Application.DTOs;
using FluentValidation;

namespace DataTransfer.Application.Validators
{
    public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
    {
        public LoginRequestDtoValidator()
        {
            RuleFor(x => x.UserName)
                .NotEmpty().WithMessage("Username is required")
                .MaximumLength(250).WithMessage("Username cannot exceed 250 characters");
                
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MaximumLength(250).WithMessage("Password cannot exceed 250 characters");
        }
    }
} 