using DataTransfer.Application.DTOs;
using FluentValidation;

namespace DataTransfer.Application.Validators
{
    public class RegisterUserDtoValidator : AbstractValidator<RegisterUserDto>
    {
        public RegisterUserDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(250).WithMessage("Name cannot exceed 250 characters");
                
            RuleFor(x => x.UserName)
                .NotEmpty().WithMessage("Username is required")
                .MaximumLength(250).WithMessage("Username cannot exceed 250 characters");
                
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters")
                .MaximumLength(250).WithMessage("Password cannot exceed 250 characters");
                
            RuleFor(x => x.EmailAddress)
                .NotEmpty().WithMessage("Email address is required")
                .EmailAddress().WithMessage("Invalid email address format")
                .MaximumLength(250).WithMessage("Email address cannot exceed 250 characters");
        }
    }
} 